; openrevo.exe SHA256=3e3b9d2297e36a0361d56c0f6357afdd9ebc8b201b943546c5cf8002cfabda09
; preferred image VA 0x140308360..0x14030852c
0000000140308360  push     r15
0000000140308362  push     r14
0000000140308364  push     r13
0000000140308366  push     r12
0000000140308368  push     rsi
0000000140308369  push     rdi
000000014030836a  push     rbx
000000014030836b  sub      rsp, 0x90
0000000140308372  mov      edi, r9d
0000000140308375  mov      rbx, r8
0000000140308378  mov      r14, rdx
000000014030837b  mov      rsi, rcx
000000014030837e  lea      rcx, [rsp + 0x28]
0000000140308383  call     0x140307c80 ; 
0000000140308388  cmp      qword ptr [rsp + 0x28], -1
000000014030838e  je       0x1403083a8
0000000140308390  mov      rax, qword ptr [rsp + 0x38]
0000000140308395  mov      qword ptr [rsi + 0x10], rax
0000000140308399  movdqu   xmm0, xmmword ptr [rsp + 0x28]
000000014030839f  movdqu   xmmword ptr [rsi], xmm0
00000001403083a3  jmp      0x1403084a6 ; 
00000001403083a8  mov      r12, qword ptr [rsp + 0xf8]
00000001403083b0  mov      r13, qword ptr [rsp + 0xf0]
00000001403083b8  test     dil, dil
00000001403083bb  je       0x1403083c4
00000001403083bd  call     0x140308100 ; 
00000001403083c2  jmp      0x1403083f3 ; 
00000001403083c4  cmp      rbx, 0x11
00000001403083c8  jne      0x1403083f3
00000001403083ca  movdqu   xmm0, xmmword ptr [r14]
00000001403083cf  pxor     xmm0, xmmword ptr [rip + 0x33f969] ; [0x140647d40] 
00000001403083d7  pshufd   xmm1, xmm0, 0xee
00000001403083dc  movq     rax, xmm1
00000001403083e1  movq     rcx, xmm0
00000001403083e6  movzx    edx, byte ptr [r14 + 0x10]
00000001403083eb  or       rdx, rcx
00000001403083ee  or       rdx, rax
00000001403083f1  je       0x1403083bd
00000001403083f3  lea      rax, [r14 + rbx]
00000001403083f7  mov      qword ptr [rsp + 0x28], r14
00000001403083fc  mov      qword ptr [rsp + 0x30], rax
0000000140308401  mov      word ptr [rsp + 0x38], 0
0000000140308408  lea      rcx, [rsp + 0x60]
000000014030840d  lea      rdx, [rsp + 0x28]
0000000140308412  call     0x1403864f0 ; 
0000000140308417  lea      rax, [rip + 0x3b3e40] ; [0x1406bc25e] '{9F33F85C-13CA-4FD1-9C4A-96217722C593}'
000000014030841e  mov      qword ptr [rsp + 0x28], rax
0000000140308423  lea      rax, [rip + 0x3b3e5b] ; [0x1406bc285] 'OemMagicVariable'
000000014030842a  mov      qword ptr [rsp + 0x30], rax
000000014030842f  mov      word ptr [rsp + 0x38], 0
0000000140308436  lea      rcx, [rsp + 0x78]
000000014030843b  lea      rdx, [rsp + 0x28]
0000000140308440  call     0x1403864f0 ; 
0000000140308445  mov      rdi, qword ptr [rsp + 0x68]
000000014030844a  mov      r15, qword ptr [rsp + 0x80]
0000000140308452  mov      rcx, rdi
0000000140308455  mov      rdx, r15
0000000140308458  mov      r8, r13
000000014030845b  mov      r9d, r12d
000000014030845e  call     0x1405c470a ; 
0000000140308463  test     eax, eax
0000000140308465  je       0x1403084bc
0000000140308467  mov      qword ptr [rsi], 0xffffffffffffffff
000000014030846e  cmp      qword ptr [rsp + 0x78], 0
0000000140308474  je       0x14030848a
0000000140308476  call     qword ptr [rip + 0x2ee17c] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030847c  mov      rcx, rax
000000014030847f  xor      edx, edx
0000000140308481  mov      r8, r15
0000000140308484  call     qword ptr [rip + 0x2ee166] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014030848a  cmp      qword ptr [rsp + 0x60], 0
0000000140308490  je       0x1403084a6
0000000140308492  call     qword ptr [rip + 0x2ee160] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
0000000140308498  mov      rcx, rax
000000014030849b  xor      edx, edx
000000014030849d  mov      r8, rdi
00000001403084a0  call     qword ptr [rip + 0x2ee14a] ; [0x1405f65f0] kernel32.dll!HeapFree
00000001403084a6  mov      rax, rsi
00000001403084a9  add      rsp, 0x90
00000001403084b0  pop      rbx
00000001403084b1  pop      rdi
00000001403084b2  pop      rsi
00000001403084b3  pop      r12
00000001403084b5  pop      r13
00000001403084b7  pop      r14
00000001403084b9  pop      r15
00000001403084bb  ret      
00000001403084bc  call     qword ptr [rip + 0x2ee36e] ; [0x1405f6830] kernel32.dll!GetLastError
00000001403084c2  mov      dword ptr [rsp + 0x4c], eax
00000001403084c6  mov      rcx, r14
00000001403084c9  mov      rdx, rbx
00000001403084cc  call     0x14005afb0 ; 
00000001403084d1  mov      qword ptr [rsp + 0x50], rax
00000001403084d6  mov      qword ptr [rsp + 0x58], rdx
00000001403084db  lea      rax, [rsp + 0x50]
00000001403084e0  mov      qword ptr [rsp + 0x28], rax
00000001403084e5  lea      rax, [rip - 0x2ebb4c] ; [0x14001c9a0] 
00000001403084ec  mov      qword ptr [rsp + 0x30], rax
00000001403084f1  lea      rax, [rsp + 0x4c]
00000001403084f6  mov      qword ptr [rsp + 0x38], rax
00000001403084fb  lea      rax, [rip - 0x2ef0d2] ; [0x140019430] 
0000000140308502  mov      qword ptr [rsp + 0x40], rax
0000000140308507  lea      rdx, [rip + 0x3b3e91] ; [0x1406bc39f] rust-format-first-literal='SetFirmwareEnvironmentVariableW failed to write '
000000014030850e  lea      r8, [rsp + 0x28]
0000000140308513  mov      rcx, rsi
0000000140308516  call     0x140003d70 ; 
000000014030851b  cmp      qword ptr [rsp + 0x78], 0
0000000140308521  jne      0x140308476
0000000140308527  jmp      0x14030848a ; 
