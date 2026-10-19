fn main() {
    println!(
        "cargo:rustc-env=TANAHUB_TARGET={}",
        std::env::var("TARGET").unwrap()
    );
    tauri_build::try_build(
        tauri_build::Attributes::new()
            .app_manifest(tauri_build::AppManifest::new().commands(&["backend_request", "save_export", "open_external", "cache_artwork"])),
    )
    .expect("Could not configure TanaHub desktop permissions");
}
