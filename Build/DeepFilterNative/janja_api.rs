// App adapter appended to upstream libDF/src/capi.rs (v0.5.6).
// Cache immutable parameters; each microphone gets its own graph and recurrent state.
#[no_mangle]
pub extern "C" fn janja_df_create() -> *mut DFState {
    use std::sync::OnceLock;
    static PARAMS: OnceLock<DfParams> = OnceLock::new();
    std::panic::catch_unwind(|| {
        let params = PARAMS.get_or_init(DfParams::default).clone();
        let runtime = RuntimeParams::default_with_ch(1)
            .with_atten_lim(100.0)
            .with_thresholds(-15.0, 35.0, 35.0)
            .with_post_filter(0.02);
        Some(Box::into_raw(Box::new(DFState(DfTract::new(params, &runtime).ok()?))))
    }).ok().flatten().unwrap_or(std::ptr::null_mut())
}
