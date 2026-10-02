; openrevo.exe SHA256=3e3b9d2297e36a0361d56c0f6357afdd9ebc8b201b943546c5cf8002cfabda09
; preferred image VA 0x140307e50..0x1403080fd
0000000140307e50  push     r15
0000000140307e52  push     r14
0000000140307e54  push     r12
0000000140307e56  push     rsi
0000000140307e57  push     rdi
0000000140307e58  push     rbx
0000000140307e59  sub      rsp, 0x88
0000000140307e60  mov      rsi, rcx
0000000140307e63  lea      rcx, [rsp + 0x20]
0000000140307e68  call     0x140307c80 ; 
0000000140307e6d  cmp      qword ptr [rsp + 0x20], -1
0000000140307e73  je       0x140307e93
0000000140307e75  mov      rax, qword ptr [rsp + 0x30]
0000000140307e7a  mov      qword ptr [rsi + 0x18], rax
0000000140307e7e  movups   xmm0, xmmword ptr [rsp + 0x20]
0000000140307e83  movups   xmmword ptr [rsi + 8], xmm0
0000000140307e87  mov      qword ptr [rsi], 0xffffffffffffffff
0000000140307e8e  jmp      0x140308085 ; 
0000000140307e93  lea      rax, [rip + 0x3b43c4] ; [0x1406bc25e] '{9F33F85C-13CA-4FD1-9C4A-96217722C593}'
0000000140307e9a  mov      qword ptr [rsp + 0x20], rax
0000000140307e9f  lea      rax, [rip + 0x3b43df] ; [0x1406bc285] 'OemMagicVariable'
0000000140307ea6  mov      qword ptr [rsp + 0x28], rax
0000000140307eab  mov      word ptr [rsp + 0x30], 0
0000000140307eb2  lea      rcx, [rsp + 0x40]
0000000140307eb7  lea      rdx, [rsp + 0x20]
0000000140307ebc  call     0x1403864f0 ; 
0000000140307ec1  mov      r8d, 0x200
0000000140307ec7  mov      edx, 8
0000000140307ecc  call     0x14050b9d0 ; 
0000000140307ed1  test     rax, rax
0000000140307ed4  je       0x1403080ec
0000000140307eda  mov      r14, rax
0000000140307edd  lea      r15, [rip + 0x3b43a1] ; [0x1406bc285] 'OemMagicVariable'
0000000140307ee4  mov      qword ptr [rsp + 0x20], r15
0000000140307ee9  lea      rax, [rip + 0x3b43a6] ; [0x1406bc296] 'UniWillVariable'
0000000140307ef0  mov      qword ptr [rsp + 0x28], rax
0000000140307ef5  mov      word ptr [rsp + 0x30], 0
0000000140307efc  lea      rcx, [rsp + 0x58]
0000000140307f01  lea      rdx, [rsp + 0x20]
0000000140307f06  call     0x1403864f0 ; 
0000000140307f0b  mov      rbx, qword ptr [rsp + 0x60]
0000000140307f10  mov      rdi, qword ptr [rsp + 0x48]
0000000140307f15  mov      rcx, rbx
0000000140307f18  mov      rdx, rdi
0000000140307f1b  mov      r8, r14
0000000140307f1e  mov      r9d, 0x200
0000000140307f24  call     0x1405c4704 ; 
0000000140307f29  test     eax, eax
0000000140307f2b  je       0x140307f39
0000000140307f2d  cmp      eax, 0x43
0000000140307f30  jbe      0x140307f97
0000000140307f32  movzx    ecx, byte ptr [r14 + 0x43]
0000000140307f37  jmp      0x140307f99 ; 
0000000140307f39  lea      r12, [rip + 0x3b4356] ; [0x1406bc296] 'UniWillVariable'
0000000140307f40  mov      qword ptr [rsp + 0x20], r12
0000000140307f45  lea      rax, [rip + 0x3b435a] ; [0x1406bc2a6] 
0000000140307f4c  mov      qword ptr [rsp + 0x28], rax
0000000140307f51  mov      word ptr [rsp + 0x30], 0
0000000140307f58  lea      rcx, [rsp + 0x70]
0000000140307f5d  lea      rdx, [rsp + 0x20]
0000000140307f62  call     0x1403864f0 ; 
0000000140307f67  mov      r15, qword ptr [rsp + 0x78]
0000000140307f6c  mov      rcx, r15
0000000140307f6f  mov      rdx, rdi
0000000140307f72  mov      r8, r14
0000000140307f75  mov      r9d, 0x200
0000000140307f7b  call     0x1405c4704 ; 
0000000140307f80  test     eax, eax
0000000140307f82  je       0x140307fe1
0000000140307f84  cmp      eax, 0x43
0000000140307f87  jbe      0x140308099
0000000140307f8d  movzx    ecx, byte ptr [r14 + 0x43]
0000000140307f92  jmp      0x14030809b ; 
0000000140307f97  xor      ecx, ecx
0000000140307f99  mov      qword ptr [rsi], 0x200
0000000140307fa0  mov      qword ptr [rsi + 8], r14
0000000140307fa4  mov      qword ptr [rsi + 0x10], 0x200
0000000140307fac  mov      qword ptr [rsi + 0x18], r15
0000000140307fb0  mov      qword ptr [rsi + 0x20], 0x11
0000000140307fb8  mov      dword ptr [rsi + 0x28], eax
0000000140307fbb  mov      byte ptr [rsi + 0x2c], 1
0000000140307fbf  mov      byte ptr [rsi + 0x2d], cl
0000000140307fc2  cmp      qword ptr [rsp + 0x58], 0
0000000140307fc8  je       0x140308069
0000000140307fce  call     qword ptr [rip + 0x2ee624] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
0000000140307fd4  mov      rcx, rax
0000000140307fd7  xor      edx, edx
0000000140307fd9  mov      r8, rbx
0000000140307fdc  jmp      0x140308063 ; 
0000000140307fe1  call     qword ptr [rip + 0x2ee849] ; [0x1405f6830] kernel32.dll!GetLastError
0000000140307fe7  mov      dword ptr [rsp + 0x3c], eax
0000000140307feb  lea      rax, [rsp + 0x3c]
0000000140307ff0  mov      qword ptr [rsp + 0x20], rax
0000000140307ff5  lea      rax, [rip - 0x2eebcc] ; [0x140019430] 
0000000140307ffc  mov      qword ptr [rsp + 0x28], rax
0000000140308001  lea      rcx, [rsi + 8]
0000000140308005  lea      rdx, [rip + 0x3b429a] ; [0x1406bc2a6] 
000000014030800c  lea      r8, [rsp + 0x20]
0000000140308011  call     0x140003d70 ; 
0000000140308016  mov      qword ptr [rsi], 0xffffffffffffffff
000000014030801d  cmp      qword ptr [rsp + 0x70], 0
0000000140308023  je       0x140308039
0000000140308025  call     qword ptr [rip + 0x2ee5cd] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030802b  mov      rcx, rax
000000014030802e  xor      edx, edx
0000000140308030  mov      r8, r15
0000000140308033  call     qword ptr [rip + 0x2ee5b7] ; [0x1405f65f0] kernel32.dll!HeapFree
0000000140308039  cmp      qword ptr [rsp + 0x58], 0
000000014030803f  je       0x140308055
0000000140308041  call     qword ptr [rip + 0x2ee5b1] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
0000000140308047  mov      rcx, rax
000000014030804a  xor      edx, edx
000000014030804c  mov      r8, rbx
000000014030804f  call     qword ptr [rip + 0x2ee59b] ; [0x1405f65f0] kernel32.dll!HeapFree
0000000140308055  call     qword ptr [rip + 0x2ee59d] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030805b  mov      rcx, rax
000000014030805e  xor      edx, edx
0000000140308060  mov      r8, r14
0000000140308063  call     qword ptr [rip + 0x2ee587] ; [0x1405f65f0] kernel32.dll!HeapFree
0000000140308069  cmp      qword ptr [rsp + 0x40], 0
000000014030806f  je       0x140308085
0000000140308071  call     qword ptr [rip + 0x2ee581] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
0000000140308077  mov      rcx, rax
000000014030807a  xor      edx, edx
000000014030807c  mov      r8, rdi
000000014030807f  call     qword ptr [rip + 0x2ee56b] ; [0x1405f65f0] kernel32.dll!HeapFree
0000000140308085  mov      rax, rsi
0000000140308088  add      rsp, 0x88
000000014030808f  pop      rbx
0000000140308090  pop      rdi
0000000140308091  pop      rsi
0000000140308092  pop      r12
0000000140308094  pop      r14
0000000140308096  pop      r15
0000000140308098  ret      
0000000140308099  xor      ecx, ecx
000000014030809b  cmp      cl, 0x19
000000014030809e  mov      qword ptr [rsi], 0x200
00000001403080a5  mov      qword ptr [rsi + 8], r14
00000001403080a9  mov      qword ptr [rsi + 0x10], 0x200
00000001403080b1  mov      qword ptr [rsi + 0x18], r12
00000001403080b5  mov      qword ptr [rsi + 0x20], 0x10
00000001403080bd  mov      dword ptr [rsi + 0x28], eax
00000001403080c0  setae    byte ptr [rsi + 0x2c]
00000001403080c4  mov      byte ptr [rsi + 0x2d], cl
00000001403080c7  cmp      qword ptr [rsp + 0x70], 0
00000001403080cd  je       0x140307fc2
00000001403080d3  call     qword ptr [rip + 0x2ee51f] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
00000001403080d9  mov      rcx, rax
00000001403080dc  xor      edx, edx
00000001403080de  mov      r8, r15
00000001403080e1  call     qword ptr [rip + 0x2ee509] ; [0x1405f65f0] kernel32.dll!HeapFree
00000001403080e7  jmp      0x140307fc2 ; 
00000001403080ec  mov      ecx, 1
00000001403080f1  mov      edx, 0x200
00000001403080f6  call     0x1405cb7af ; 
00000001403080fb  ud2      
