use serde::Deserialize;
use serde_json::{json, Value};
use std::{
    io::{BufRead, BufReader, Read, Write},
    path::PathBuf,
    process::{Child, Command, Stdio},
    sync::{
        atomic::{AtomicBool, Ordering},
        mpsc::{self, Receiver},
        Arc, Mutex,
    },
    time::Duration,
};
use tauri::Manager;
use tauri_plugin_dialog::DialogExt;
use tauri_plugin_opener::OpenerExt;
mod artwork;

const REQUEST_TIMEOUT: Duration = Duration::from_secs(45);
const MAX_RESPONSE_BYTES: u64 = 8 * 1024 * 1024;

#[derive(Deserialize)]
struct BridgeResponse {
    id: String,
    result: Option<Value>,
    error: Option<BridgeError>,
}

#[derive(Deserialize)]
struct BridgeError {
    message: String,
}

struct BridgeProcess {
    child: Arc<Mutex<Child>>,
    requests: mpsc::SyncSender<Vec<u8>>,
    responses: Receiver<Result<BridgeResponse, String>>,
}

impl Drop for BridgeProcess {
    fn drop(&mut self) {
        if let Ok(mut child) = self.child.lock() {
            let _ = child.kill();
            let _ = child.wait();
        }
    }
}

#[derive(Default)]
struct BridgeSession {
    process: Option<BridgeProcess>,
    sequence: u64,
}

#[derive(Default)]
struct BridgeInner {
    session: Mutex<BridgeSession>,
    // Separate from the request lock so closing the window can interrupt a pending request.
    running_child: Mutex<Option<Arc<Mutex<Child>>>>,
    closing: AtomicBool,
}

#[derive(Clone, Default)]
struct BridgeState(Arc<BridgeInner>);

#[derive(Default)]
struct AuthBridgeState(BridgeState);

fn bridge_binary() -> Result<PathBuf, String> {
    let extension = if cfg!(windows) { ".exe" } else { "" };
    if cfg!(debug_assertions) {
        let development_binary = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("binaries")
            .join(format!(
                "TanaHub.Bridge-{}{extension}",
                env!("TANAHUB_TARGET")
            ));
        if development_binary.is_file() {
            return Ok(development_binary);
        }
    }

    // Tauri bundles externalBin next to the app executable, stripping the target suffix.
    let executable = std::env::current_exe()
        .map_err(|error| format!("Cannot locate the TanaHub application: {error}"))?;
    let binary = executable
        .parent()
        .ok_or("Cannot locate the TanaHub application directory")?
        .join(format!("TanaHub.Bridge{extension}"));
    if !binary.is_file() {
        return Err("TanaHub core is missing. For development, run npm run prepare:bridge before launching the desktop app.".into());
    }
    Ok(binary)
}

impl BridgeState {
    fn spawn(&self) -> Result<BridgeProcess, String> {
        let mut command = Command::new(bridge_binary()?);
        command
            .stdin(Stdio::piped())
            .stdout(Stdio::piped())
            .stderr(Stdio::piped());
        #[cfg(windows)]
        {
            use std::os::windows::process::CommandExt;
            command.creation_flags(0x0800_0000);
        }
        let mut child = command
            .spawn()
            .map_err(|error| format!("TanaHub core could not start: {error}"))?;
        let mut stdin = child.stdin.take().ok_or("TanaHub core has no input pipe")?;
        let stdout = child
            .stdout
            .take()
            .ok_or("TanaHub core has no output pipe")?;
        let stderr = child
            .stderr
            .take()
            .ok_or("TanaHub core has no error pipe")?;
        let child = Arc::new(Mutex::new(child));
        let (sender, responses) = mpsc::sync_channel(1);
        let reader_sender = sender.clone();
        let (requests, incoming_requests) = mpsc::sync_channel::<Vec<u8>>(1);
        std::thread::spawn(move || {
            for bytes in incoming_requests {
                if let Err(error) = stdin.write_all(&bytes).and_then(|_| stdin.flush()) {
                    let _ = sender.send(Err(format!(
                        "Cannot send the request to TanaHub core: {error}"
                    )));
                    break;
                }
            }
        });
        std::thread::spawn(move || {
            let mut reader = BufReader::new(stdout);
            loop {
                let mut bytes = Vec::new();
                let response = match (&mut reader)
                    .take(MAX_RESPONSE_BYTES + 1)
                    .read_until(b'\n', &mut bytes)
                {
                    Ok(0) => Err("TanaHub core stopped unexpectedly. Try the action again.".into()),
                    Ok(_) if bytes.len() as u64 > MAX_RESPONSE_BYTES => Err(
                        "TanaHub core returned a response larger than the supported limit.".into(),
                    ),
                    Ok(_) => serde_json::from_slice(&bytes).map_err(|error| {
                        format!("TanaHub core returned an invalid response: {error}")
                    }),
                    Err(error) => Err(format!("Cannot read from TanaHub core: {error}")),
                };
                let failed = response.is_err();
                if reader_sender.send(response).is_err() || failed {
                    break;
                }
            }
        });
        std::thread::spawn(move || {
            // Drain diagnostics without mixing them with the line-based response protocol.
            for line in BufReader::new(stderr).lines().map_while(Result::ok) {
                eprintln!("[TanaHub core] {line}");
            }
        });
        *self
            .0
            .running_child
            .lock()
            .map_err(|_| "TanaHub core state is unavailable")? = Some(child.clone());
        let process = BridgeProcess {
            child,
            requests,
            responses,
        };
        if self.0.closing.load(Ordering::SeqCst) {
            return Err("TanaHub is closing".into());
        }
        Ok(process)
    }

    fn request(&self, method: String, params: Value) -> Result<Value, String> {
        if self.0.closing.load(Ordering::SeqCst) {
            return Err("TanaHub is closing".into());
        }
        if method.is_empty() || method.len() > 100 || !params.is_object() {
            return Err("Invalid TanaHub request".into());
        }
        let mut session = self
            .0
            .session
            .lock()
            .map_err(|_| "TanaHub core state is unavailable")?;
        if self.0.closing.load(Ordering::SeqCst) {
            return Err("TanaHub is closing".into());
        }
        if let Some(process) = &session.process {
            let stopped = process
                .child
                .lock()
                .map_err(|_| "TanaHub core process state is unavailable")?
                .try_wait()
                .map_err(|error| format!("Cannot check TanaHub core: {error}"))?
                .is_some();
            if stopped {
                session.process = None;
            }
        }
        if session.process.is_none() {
            session.process = Some(self.spawn()?);
        }
        session.sequence += 1;
        let id = session.sequence.to_string();
        let timeout = match method.as_str() {
            "anilist.connect" => Duration::from_secs(360),
            "anilist.sync" => Duration::from_secs(300),
            "recognition.run" => Duration::from_secs(120),
            _ => REQUEST_TIMEOUT,
        };
        let request = json!({ "id": id, "method": method, "params": params });
        let mut bytes = serde_json::to_vec(&request).map_err(|error| error.to_string())?;
        if bytes.len() > 20 * 1024 * 1024 {
            return Err("TanaHub request is too large.".into());
        }
        bytes.push(b'\n');
        let process = session
            .process
            .as_mut()
            .ok_or("TanaHub core is unavailable")?;
        if let Err(error) = process.requests.try_send(bytes) {
            session.process = None;
            return Err(format!("Cannot send the request to TanaHub core: {error}"));
        }
        let response = match process.responses.recv_timeout(timeout) {
            Ok(Ok(response)) => response,
            Ok(Err(error)) => {
                session.process = None;
                return Err(error);
            }
            Err(mpsc::RecvTimeoutError::Timeout) => {
                session.process = None;
                return Err(
                    format!("TanaHub core did not respond within {} seconds. Try the action again.", timeout.as_secs()),
                );
            }
            Err(mpsc::RecvTimeoutError::Disconnected) => {
                session.process = None;
                return Err("TanaHub core disconnected. Try the action again.".into());
            }
        };
        if response.id != id {
            session.process = None;
            return Err(
                "TanaHub core response did not match the request. Try the action again.".into(),
            );
        }
        if let Some(error) = response.error {
            return Err(error.message);
        }
        Ok(response.result.unwrap_or(Value::Null))
    }

    fn shutdown(&self) {
        self.0.closing.store(true, Ordering::SeqCst);
        if let Ok(running_child) = self.0.running_child.lock() {
            if let Some(child) = &*running_child {
                if let Ok(mut child) = child.lock() {
                    let _ = child.kill();
                    let _ = child.wait();
                }
            }
        }
    }
}

#[tauri::command]
async fn backend_request(
    state: tauri::State<'_, BridgeState>,
    auth: tauri::State<'_, AuthBridgeState>,
    method: String,
    params: Value,
) -> Result<Value, String> {
    // OAuth waits for a user in their browser; keep ordinary library requests responsive.
    let bridge = if method == "anilist.connect" { auth.0.clone() } else { state.inner().clone() };
    tauri::async_runtime::spawn_blocking(move || bridge.request(method, params))
        .await
        .map_err(|error| format!("TanaHub core request failed: {error}"))?
}

#[tauri::command]
async fn save_export(app: tauri::AppHandle, text: String, file_name: String, format: String) -> Result<bool, String> {
    if text.len() > 8 * 1024 * 1024 || !matches!(format.as_str(), "csv" | "xml") {
        return Err("Unsupported library export.".into());
    }
    if file_name.is_empty() || file_name.len() > 100 || file_name.contains(['/', '\\']) {
        return Err("Invalid export name.".into());
    }
    tauri::async_runtime::spawn_blocking(move || {
        let selected = app.dialog().file().set_title("Export your library")
            .set_file_name(file_name).add_filter("Library export", &[format.as_str()]).blocking_save_file();
        let Some(selected) = selected else { return Ok(false); };
        let path = selected.into_path().map_err(|_| "Choose a local file to save the export.")?;
        std::fs::write(path, text).map_err(|_| "Could not save the export. Choose another location.")?;
        Ok(true)
    }).await.map_err(|_| "Could not open the save dialog.")?
}

#[tauri::command]
fn open_external(app: tauri::AppHandle, url: String) -> Result<(), String> {
    let parsed = tauri::Url::parse(&url).map_err(|_| "Invalid link.")?;
    if !matches!(parsed.scheme(), "https" | "http") || parsed.host_str().is_none()
        || !parsed.username().is_empty() || parsed.password().is_some() || url.len() > 4096 {
        return Err("This link cannot be opened.".into());
    }
    app.opener().open_url(parsed.as_str(), None::<&str>).map_err(|_| "Could not open the browser.".into())
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let app = tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_clipboard_manager::init())
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .plugin(tauri_plugin_process::init())
        .manage(BridgeState::default())
        .manage(AuthBridgeState::default())
        .setup(|app| {
            let root = std::env::var_os("TANAHUB_DATA_DIRECTORY")
                .map(PathBuf::from).filter(|path| path.is_absolute())
                .map(|path| path.join("artwork-cache"))
                .or_else(|| app.path().app_cache_dir().ok().map(|path| path.join("artwork")));
            let root = root.filter(|path| std::fs::create_dir_all(path).is_ok()
                && app.asset_protocol_scope().allow_directory(path, true).is_ok());
            app.manage(artwork::ArtworkCache::new(root));
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![backend_request, save_export, open_external, cache_artwork, update_support, prepare_update, resume_after_update])
        .build(tauri::generate_context!())
        .expect("Could not initialize the TanaHub desktop window");
    app.run(|handle, event| {
        if matches!(
            event,
            tauri::RunEvent::Exit | tauri::RunEvent::ExitRequested { .. }
        ) {
            handle.state::<BridgeState>().shutdown();
            handle.state::<AuthBridgeState>().0.shutdown();
        }
    });
}

#[tauri::command]
async fn cache_artwork(cache: tauri::State<'_, artwork::ArtworkCache>, url: String) -> Result<Option<String>, String> {
    cache.resolve(&url).await
}

#[tauri::command]
fn update_support() -> Value {
    if cfg!(debug_assertions) {
        json!({"supported": false, "reason": "In-app installation is available in packaged desktop builds. Run a release package or use Browse releases."})
    } else if cfg!(target_os = "linux") && std::env::var_os("APPIMAGE").is_none() {
        json!({"supported": false, "reason": "In-app updates on Linux require the AppImage. Update the DEB version through your package manager or Browse releases."})
    } else {
        json!({"supported": true, "reason": null})
    }
}

#[tauri::command]
async fn prepare_update(state: tauri::State<'_, BridgeState>, auth: tauri::State<'_, AuthBridgeState>) -> Result<(), String> {
    let state = state.inner().clone();
    let auth = auth.0.clone();
    tauri::async_runtime::spawn_blocking(move || {
        pause_for_update(&state, &auth)
    }).await.map_err(|_| "Could not prepare the application for updating.")?
}

#[tauri::command]
fn resume_after_update(state: tauri::State<'_, BridgeState>, auth: tauri::State<'_, AuthBridgeState>) {
    state.0.closing.store(false, Ordering::SeqCst);
    auth.0.0.closing.store(false, Ordering::SeqCst);
}

fn pause_for_update(state: &BridgeState, auth: &BridgeState) -> Result<(), String> {
    // Never replace a running sidecar or interrupt an in-flight save/import/OAuth request.
    let mut normal = state.0.session.try_lock().map_err(|_| "Finish the current library operation, then try again.")?;
    let mut authentication = auth.0.session.try_lock().map_err(|_| "Finish connecting AniList, then try again.")?;
    state.0.closing.store(true, Ordering::SeqCst);
    auth.0.closing.store(true, Ordering::SeqCst);
    normal.process = None;
    authentication.process = None;
    state.0.running_child.lock().map_err(|_| "Could not release the core.")?.take();
    auth.0.running_child.lock().map_err(|_| "Could not release authentication.")?.take();
    Ok(())
}

#[cfg(test)]
mod update_tests {
    use super::*;
    #[test]
    fn updating_waits_for_existing_library_and_authentication_operations() {
        let state = BridgeState::default();
        let auth = BridgeState::default();
        let library_request = state.0.session.lock().unwrap();
        assert!(pause_for_update(&state, &auth).is_err());
        assert!(!state.0.closing.load(Ordering::SeqCst));
        drop(library_request);
        let authentication = auth.0.session.lock().unwrap();
        assert!(pause_for_update(&state, &auth).is_err());
        assert!(!state.0.closing.load(Ordering::SeqCst));
        drop(authentication);
        assert!(pause_for_update(&state, &auth).is_ok());
        assert!(state.0.closing.load(Ordering::SeqCst));
        assert!(auth.0.closing.load(Ordering::SeqCst));
    }
}
