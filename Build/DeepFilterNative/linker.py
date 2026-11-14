from pathlib import Path
import subprocess,sys
root=Path(__file__).resolve().parent
toolchain=root/'rustup/toolchains/1.100.0-x86_64-pc-windows-gnu'
if not toolchain.exists(): toolchain=root/'rustup/toolchains/stable-x86_64-pc-windows-gnu'
lld=toolchain/'lib/rustlib/x86_64-pc-windows-gnu/bin/rust-lld.exe'
def expand(args):
 result=[]
 for a in args:
  if a.startswith('@'):
   raw=Path(a[1:]).read_bytes();s=raw.decode('utf-16') if raw.startswith(b'\xff\xfe') else raw.decode('utf-8-sig')
   result.extend(__import__('shlex').split(s,posix=True))
  else: result.append(a)
 return result
args=['-m','i386pep','-L'+str(lld.parent.parent/'lib/self-contained')]
for a in expand(sys.argv[1:]):
 if a in ['-fno-use-linker-plugin','-m64','-nostartfiles','-nodefaultlibs']: continue
 if a.startswith('-Wl,'): args.extend(a[4:].split(','))
 else: args.append(a)
if not any(a.endswith(('crt2.o','dllcrt2.o')) for a in args):
 args.append(str(lld.parent.parent/'lib/self-contained'/('dllcrt2.o' if '-shared' in args else 'crt2.o')))
# Avoid the Windows command length limit for large native links.
rsp=root/('link-'+str(__import__('os').getpid())+'.rsp')
rsp.write_text('\n'.join('"'+a.replace('\\','/')+'"' for a in args),encoding='utf-8')
try: sys.exit(subprocess.call([str(lld),'-flavor','gnu','@'+str(rsp)]))
finally: rsp.unlink(missing_ok=True)
