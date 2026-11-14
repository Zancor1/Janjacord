from pathlib import Path
import sys

source = Path(sys.argv[1])
adapter = Path(__file__).resolve().parent
workspace = source / 'Cargo.toml'
text = workspace.read_text(encoding='utf-8')
start = text.index('members = [')
end = text.index(']', start)
workspace.write_text(text[:start] + 'members = ["libDF"]' + text[end+1:], encoding='utf-8')
manifest = source / 'libDF/Cargo.toml'
text = manifest.read_text(encoding='utf-8')
if 'crate-type =' not in text:
    text = text.replace('path = "src/lib.rs"', 'path = "src/lib.rs"\ncrate-type = ["cdylib", "rlib"]')
# Training-only dependency is not built. Avoid requiring Git just to resolve it.
text = text.replace('git = "https://github.com/aldanor/hdf5-rust.git", rev = "26046fb"', 'version = "0.8.1"')
text = text.replace('hdf5-static = ["hdf5?/static"]', 'hdf5-static = []')
manifest.write_text(text, encoding='utf-8')
capi = source / 'libDF/src/capi.rs'
text = capi.read_text(encoding='utf-8')
text = text.replace('state.0.process(input, output).expect("Failed to process DF frame")',
    'std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| state.0.process(input, output)))\n'
    '        .ok().and_then(Result::ok).unwrap_or(f32::NAN)')
if '// App adapter appended' in text:
    text = text[:text.index('// App adapter appended')]
text += '\n' + (adapter / 'janja_api.rs').read_text(encoding='utf-8')
capi.write_text(text, encoding='utf-8')
