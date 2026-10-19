use reqwest::{redirect::Policy, Client, Url};
use sha2::{Digest, Sha256};
use std::{
    fs,
    io::{Read, Write},
    path::{Path, PathBuf},
    time::{Duration, SystemTime},
};
use tokio::sync::Semaphore;

const MAX_IMAGE: usize = 8 * 1024 * 1024;
const CACHE_BUDGET: u64 = 256 * 1024 * 1024;

// Fetch only provider-owned image hosts. Never turn catalog data into a general HTTP proxy.
fn allowed(url: &Url) -> bool {
    url.scheme() == "https"
        && url.port_or_known_default() == Some(443)
        && url.username().is_empty()
        && url.password().is_none()
        && matches!(
            url.host_str(),
            Some("s4.anilist.co" | "s.anilist.co" | "uploads.mangadex.org" | "cdn.myanimelist.net")
        )
}

fn key(url: &str) -> String {
    format!("{:x}.img", Sha256::digest(url.as_bytes()))
}

fn raster(bytes: &[u8]) -> bool {
    bytes.starts_with(&[0xff, 0xd8, 0xff])
        || bytes.starts_with(b"\x89PNG\r\n\x1a\n")
        || bytes.starts_with(b"GIF87a")
        || bytes.starts_with(b"GIF89a")
        || (bytes.starts_with(b"RIFF") && bytes.get(8..12) == Some(b"WEBP"))
}

fn hit(path: &Path) -> bool {
    let Ok(mut file) = fs::File::open(path) else {
        return false;
    };
    let Ok(metadata) = file.metadata() else {
        return false;
    };
    if metadata.len() > MAX_IMAGE as u64 {
        return false;
    }
    let mut header = [0; 12];
    let count = metadata.len().min(12) as usize;
    if file.read_exact(&mut header[..count]).is_err() || !raster(&header[..count]) {
        return false;
    }
    let _ = file.set_modified(SystemTime::now());
    true
}

fn prune(root: &Path, budget: u64) -> std::io::Result<()> {
    let mut entries = Vec::new();
    let mut size = 0;
    for entry in fs::read_dir(root)? {
        let entry = entry?;
        if entry.path().extension().and_then(|s| s.to_str()) != Some("img") {
            continue;
        }
        let metadata = entry.metadata()?;
        if !metadata.is_file() {
            continue;
        }
        size += metadata.len();
        entries.push((metadata.modified()?, entry.path(), metadata.len()));
    }
    entries.sort_by_key(|entry| entry.0);
    for (_, path, length) in entries {
        if size <= budget {
            break;
        }
        fs::remove_file(path)?;
        size -= length;
    }
    Ok(())
}

pub struct ArtworkCache {
    root: Option<PathBuf>,
    client: Result<Client, reqwest::Error>,
    downloads: Semaphore,
}

impl ArtworkCache {
    pub fn new(root: Option<PathBuf>) -> Self {
        let client = Client::builder()
            .https_only(true)
            .timeout(Duration::from_secs(12))
            .redirect(Policy::custom(|attempt| {
                if attempt.previous().len() >= 3 || !allowed(attempt.url()) {
                    attempt.stop()
                } else {
                    attempt.follow()
                }
            }))
            .build();
        Self {
            root,
            client,
            downloads: Semaphore::new(4),
        }
    }

    pub async fn resolve(&self, value: &str) -> Result<Option<String>, String> {
        if value.len() > 4096 {
            return Ok(None);
        }
        let url = Url::parse(value).map_err(|_| "Invalid artwork URL.")?;
        if !allowed(&url) {
            return Ok(None);
        }
        let Some(root) = &self.root else {
            return Ok(None);
        };
        let path = root.join(key(url.as_str()));
        let _permit = self
            .downloads
            .acquire()
            .await
            .map_err(|_| "Artwork loading stopped.")?;
        let check = path.clone();
        if tauri::async_runtime::spawn_blocking(move || hit(&check))
            .await
            .unwrap_or(false)
        {
            return Ok(Some(path.to_string_lossy().into_owned()));
        }
        let client = self
            .client
            .as_ref()
            .map_err(|_| "Artwork is unavailable.")?;
        let mut response = client
            .get(url)
            .send()
            .await
            .map_err(|_| "Could not load artwork.")?
            .error_for_status()
            .map_err(|_| "Could not load artwork.")?;
        if response
            .content_length()
            .is_some_and(|length| length > MAX_IMAGE as u64)
        {
            return Err("Artwork is too large.".into());
        }
        let mut bytes = Vec::new();
        while let Some(chunk) = response
            .chunk()
            .await
            .map_err(|_| "Could not load artwork.")?
        {
            if bytes.len() + chunk.len() > MAX_IMAGE {
                return Err("Artwork is too large.".into());
            }
            bytes.extend_from_slice(&chunk);
        }
        if !raster(&bytes) {
            return Err("Unsupported artwork format.".into());
        }
        let root = root.clone();
        let destination = path.clone();
        tauri::async_runtime::spawn_blocking(move || -> std::io::Result<()> {
            fs::create_dir_all(&root)?;
            let mut temporary = tempfile::NamedTempFile::new_in(&root)?;
            temporary.write_all(&bytes)?;
            temporary
                .persist(destination)
                .map_err(|error| error.error)?;
            prune(&root, CACHE_BUDGET)
        })
        .await
        .map_err(|_| "Could not cache artwork.")?
        .map_err(|_| "Could not cache artwork.")?;
        Ok(Some(path.to_string_lossy().into_owned()))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn only_provider_https_hosts_and_default_ports_can_be_fetched() {
        assert!(allowed(
            &Url::parse("https://s4.anilist.co/file/cover.jpg").unwrap()
        ));
        for url in [
            "http://s4.anilist.co/x",
            "https://localhost/x",
            "https://127.0.0.1/x",
            "https://s4.anilist.co.evil.test/x",
            "https://user@s4.anilist.co/x",
            "https://s4.anilist.co:8443/x",
        ] {
            assert!(!allowed(&Url::parse(url).unwrap()), "{url}");
        }
    }

    #[test]
    fn cache_keys_preserve_query_variants_and_cannot_escape_the_cache() {
        let name = key("https://s4.anilist.co/../../cover?a=1");
        assert_eq!(name.len(), 68);
        assert!(!name.contains('/'));
        assert_ne!(name, key("https://s4.anilist.co/../../cover?a=2"));
    }

    #[test]
    fn cached_artwork_survives_cache_recreation_and_invalid_bytes_are_rejected() {
        let root = tempfile::tempdir().unwrap();
        let path = root.path().join(key("https://s4.anilist.co/cover.jpg"));
        fs::write(&path, b"\x89PNG\r\n\x1a\nfixture").unwrap();
        assert!(hit(&path));
        let mut restarted = ArtworkCache::new(Some(root.path().to_path_buf()));
        restarted.client = Client::builder().user_agent("\n").build();
        assert!(restarted.client.is_err()); // A disk hit must not need a working HTTP client.
        let runtime = tokio::runtime::Builder::new_current_thread()
            .enable_all()
            .build()
            .unwrap();
        let result = runtime
            .block_on(restarted.resolve("https://s4.anilist.co/cover.jpg"))
            .unwrap();
        assert_eq!(result, Some(path.to_string_lossy().into_owned()));
        fs::write(&path, b"<html>provider error</html>").unwrap();
        assert!(!hit(&path));
        assert!(!raster(b"<svg xmlns='http://www.w3.org/2000/svg'/>"));
    }

    #[test]
    fn eviction_keeps_recently_used_images_and_ignores_unrelated_files() {
        let root = tempfile::tempdir().unwrap();
        let old = root.path().join("old.img");
        let recent = root.path().join("recent.img");
        fs::write(&old, b"\xff\xd8\xffold").unwrap();
        fs::File::open(&old)
            .unwrap()
            .set_modified(SystemTime::UNIX_EPOCH)
            .unwrap();
        fs::write(&recent, b"\xff\xd8\xffnew").unwrap();
        fs::write(root.path().join("keep.txt"), b"unrelated").unwrap();
        prune(root.path(), 6).unwrap();
        assert!(!old.exists());
        assert!(recent.exists());
        assert!(root.path().join("keep.txt").exists());
    }
}
