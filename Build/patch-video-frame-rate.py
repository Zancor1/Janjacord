"""Reproduce the JanjaCord 60 FPS variant of MR-WebRTC 2.0.2 win-x64.

Upstream external_video_track_source.cpp:288 schedules now+30ms.
At RVA 0x9b2a6 the shipped instruction is lea r8,[r14+0x1e].
Change the delay to 8ms; the application still supplies only selected-rate frames.
No changes are made to the NuGet cache or existing application installations.
"""
from pathlib import Path
import hashlib
import struct
import sys

root=Path(__file__).resolve().parent.parent
source=Path(sys.argv[1]) if len(sys.argv)>1 else root/'.tools/packages/microsoft.mixedreality.webrtc/2.0.2/runtimes/win10-x64/native/mrwebrtc.dll'
data=bytearray(source.read_bytes())
expected='203a747d06d391c180cba0614fabfc54b16e279e0280e9c0ade2f27c6348d2b5'
if hashlib.sha256(data).hexdigest()!=expected:
    raise SystemExit('Unsupported input DLL: SHA-256 does not match the inspected 2.0.2 image')
pe=struct.unpack_from('<I',data,0x3c)[0]
optional=pe+24
if data[pe:pe+4]!=b'PE\0\0' or struct.unpack_from('<H',data,optional)[0]!=0x20b:
    raise SystemExit('Expected PE32+ image')
offset=0x9a6a6
if data[offset:offset+4]!=bytes.fromhex('4d8d461e'):
    raise SystemExit('Expected external-source scheduling instruction not found')
data[offset+3]=8
# This is a modified, unsigned build. Remove the original Authenticode table
# instead of retaining a Microsoft signature which would no longer verify.
security=optional+112+4*8
cert_offset,cert_size=struct.unpack_from('<II',data,security)
if cert_offset+cert_size!=len(data) or cert_offset!=7878144:
    raise SystemExit('Unexpected certificate layout')
struct.pack_into('<II',data,security,0,0)
del data[cert_offset:]
checksum_offset=optional+64
struct.pack_into('<I',data,checksum_offset,0)
checksum=0
for index in range(0,len(data),2):
    checksum+=data[index]+((data[index+1] if index+1<len(data) else 0)<<8)
    checksum=(checksum&0xffff)+(checksum>>16)
checksum=(checksum&0xffff)+(checksum>>16)
struct.pack_into('<I',data,checksum_offset,checksum+len(data))
destination=root/'Client/Native/mrwebrtc.dll'
destination.write_bytes(data)
print(hashlib.sha256(data).hexdigest(),destination)
