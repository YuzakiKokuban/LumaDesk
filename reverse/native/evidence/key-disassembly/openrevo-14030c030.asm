; openrevo.exe SHA256=3e3b9d2297e36a0361d56c0f6357afdd9ebc8b201b943546c5cf8002cfabda09
; preferred image VA 0x14030c030..0x14030c392
000000014030c030  push     r14
000000014030c032  push     rsi
000000014030c033  push     rdi
000000014030c034  push     rbx
000000014030c035  sub      rsp, 0x98
000000014030c03c  mov      word ptr [rsp + 0x4e], cx
000000014030c041  movzx    eax, byte ptr [rip + 0x46d5fa] ; [0x140779642] 
000000014030c048  test     al, al
000000014030c04a  je       0x14030c1fd
000000014030c050  movzx    eax, byte ptr [rip + 0x46d5ec] ; [0x140779643] 
000000014030c057  test     al, al
000000014030c059  je       0x14030c20a
000000014030c05f  mov      r8d, 0xf
000000014030c065  xor      edx, edx
000000014030c067  call     0x14050b9d0 ; 
000000014030c06c  test     rax, rax
000000014030c06f  je       0x14030c381
000000014030c075  mov      rsi, rax
000000014030c078  movabs   rax, 0x495043415c2e5c5c
000000014030c082  mov      qword ptr [rsi], rax
000000014030c085  mov      dword ptr [rsi + 8], 0x76697244
000000014030c08c  mov      word ptr [rsi + 0xc], 0x7265
000000014030c092  mov      byte ptr [rsi + 0xe], 0
000000014030c096  mov      qword ptr [rsp + 0x30], 0
000000014030c09f  mov      dword ptr [rsp + 0x28], 0
000000014030c0a7  mov      dword ptr [rsp + 0x20], 3
000000014030c0af  mov      rcx, rsi
000000014030c0b2  mov      edx, 0xc0000000
000000014030c0b7  mov      r8d, 3
000000014030c0bd  xor      r9d, r9d
000000014030c0c0  call     qword ptr [rip + 0x2ea6fa] ; [0x1405f67c0] kernel32.dll!CreateFileA
000000014030c0c6  lea      rcx, [rax - 1]
000000014030c0ca  cmp      rcx, -2
000000014030c0ce  jae      0x14030c211
000000014030c0d4  movzx    ecx, word ptr [rsp + 0x4e]
000000014030c0d9  mov      dword ptr [rsp + 0x70], ecx
000000014030c0dd  mov      dword ptr [rsp + 0x54], 0
000000014030c0e5  mov      dword ptr [rsp + 0x74], 0
000000014030c0ed  lea      rcx, [rsp + 0x74]
000000014030c0f2  mov      qword ptr [rsp + 0x30], rcx
000000014030c0f7  lea      rcx, [rsp + 0x54]
000000014030c0fc  mov      qword ptr [rsp + 0x20], rcx
000000014030c101  mov      qword ptr [rsp + 0x38], 0
000000014030c10a  mov      dword ptr [rsp + 0x28], 4
000000014030c112  lea      r8, [rsp + 0x70]
000000014030c117  mov      rcx, rax
000000014030c11a  mov      edx, 0x9c40a488
000000014030c11f  mov      r9d, 4
000000014030c125  mov      rdi, rax
000000014030c128  call     qword ptr [rip + 0x2ea68a] ; [0x1405f67b8] kernel32.dll!DeviceIoControl
000000014030c12e  mov      ebx, eax
000000014030c130  call     qword ptr [rip + 0x2ea6fa] ; [0x1405f6830] kernel32.dll!GetLastError
000000014030c136  mov      dword ptr [rsp + 0x50], eax
000000014030c13a  mov      rcx, rdi
000000014030c13d  call     qword ptr [rip + 0x2ea495] ; [0x1405f65d8] kernel32.dll!CloseHandle
000000014030c143  test     ebx, ebx
000000014030c145  setne    dil
000000014030c149  je       0x14030c2dd
000000014030c14f  movzx    eax, byte ptr [rsp + 0x54]
000000014030c154  mov      byte ptr [rsp + 0x4d], al
000000014030c158  lea      rax, [rsp + 0x4e]
000000014030c15d  mov      qword ptr [rsp + 0x78], rax
000000014030c162  lea      rax, [rip - 0x2f34f9] ; [0x140018c70] 
000000014030c169  mov      qword ptr [rsp + 0x80], rax
000000014030c171  lea      rax, [rsp + 0x4d]
000000014030c176  mov      qword ptr [rsp + 0x88], rax
000000014030c17e  lea      rax, [rip - 0x2f4dd5] ; [0x1400173b0] 
000000014030c185  mov      qword ptr [rsp + 0x90], rax
000000014030c18d  lea      rdx, [rip + 0x3b1ed8] ; [0x1406be06c] rust-format-first-literal='raw_driver_read_ec('
000000014030c194  lea      rcx, [rsp + 0x58]
000000014030c199  lea      r8, [rsp + 0x78]
000000014030c19e  call     0x140003d70 ; 
000000014030c1a3  mov      rbx, qword ptr [rsp + 0x60]
000000014030c1a8  mov      rax, qword ptr [rsp + 0x68]
000000014030c1ad  mov      r14, qword ptr [rsp + 0x58]
000000014030c1b2  mov      qword ptr [rsp + 0x28], rax
000000014030c1b7  mov      qword ptr [rsp + 0x20], rbx
000000014030c1bc  lea      rcx, [rip + 0x352396] ; [0x14065e559] 
000000014030c1c3  lea      r8, [rip + 0x3b1b96] ; [0x1406bdd60] 
000000014030c1ca  mov      edx, 5
000000014030c1cf  mov      r9d, 9
000000014030c1d5  call     0x1403409f0 ; 
000000014030c1da  test     r14, r14
000000014030c1dd  je       0x14030c1f3
000000014030c1df  call     qword ptr [rip + 0x2ea413] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030c1e5  mov      rcx, rax
000000014030c1e8  xor      edx, edx
000000014030c1ea  mov      r8, rbx
000000014030c1ed  call     qword ptr [rip + 0x2ea3fd] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014030c1f3  movzx    ebx, byte ptr [rsp + 0x4d]
000000014030c1f8  jmp      0x14030c2b8 ; 
000000014030c1fd  call     0x14030ea10 ; 
000000014030c202  test     al, al
000000014030c204  jne      0x14030c05f
000000014030c20a  xor      edi, edi
000000014030c20c  jmp      0x14030c2cc ; 
000000014030c211  call     qword ptr [rip + 0x2ea619] ; [0x1405f6830] kernel32.dll!GetLastError
000000014030c217  mov      dword ptr [rsp + 0x50], eax
000000014030c21b  lea      rax, [rsp + 0x4e]
000000014030c220  mov      qword ptr [rsp + 0x78], rax
000000014030c225  lea      rax, [rip - 0x2f35bc] ; [0x140018c70] 
000000014030c22c  mov      qword ptr [rsp + 0x80], rax
000000014030c234  lea      rax, [rsp + 0x50]
000000014030c239  mov      qword ptr [rsp + 0x88], rax
000000014030c241  lea      rax, [rip - 0x2f2e18] ; [0x140019430] 
000000014030c248  mov      qword ptr [rsp + 0x90], rax
000000014030c250  lea      rdx, [rip + 0x3b1d42] ; [0x1406bdf99] rust-format-first-literal='raw_driver_read_ec('
000000014030c257  lea      rcx, [rsp + 0x58]
000000014030c25c  lea      r8, [rsp + 0x78]
000000014030c261  call     0x140003d70 ; 
000000014030c266  mov      rdi, qword ptr [rsp + 0x60]
000000014030c26b  mov      rax, qword ptr [rsp + 0x68]
000000014030c270  mov      rbx, qword ptr [rsp + 0x58]
000000014030c275  mov      qword ptr [rsp + 0x28], rax
000000014030c27a  mov      qword ptr [rsp + 0x20], rdi
000000014030c27f  lea      rcx, [rip + 0x3522d3] ; [0x14065e559] 
000000014030c286  lea      r8, [rip + 0x3b1ad3] ; [0x1406bdd60] 
000000014030c28d  mov      edx, 5
000000014030c292  mov      r9d, 9
000000014030c298  call     0x1403409f0 ; 
000000014030c29d  test     rbx, rbx
000000014030c2a0  je       0x14030c2b6
000000014030c2a2  call     qword ptr [rip + 0x2ea350] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030c2a8  mov      rcx, rax
000000014030c2ab  xor      edx, edx
000000014030c2ad  mov      r8, rdi
000000014030c2b0  call     qword ptr [rip + 0x2ea33a] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014030c2b6  xor      edi, edi
000000014030c2b8  call     qword ptr [rip + 0x2ea33a] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030c2be  mov      rcx, rax
000000014030c2c1  xor      edx, edx
000000014030c2c3  mov      r8, rsi
000000014030c2c6  call     qword ptr [rip + 0x2ea324] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014030c2cc  mov      eax, edi
000000014030c2ce  mov      edx, ebx
000000014030c2d0  add      rsp, 0x98
000000014030c2d7  pop      rbx
000000014030c2d8  pop      rdi
000000014030c2d9  pop      rsi
000000014030c2da  pop      r14
000000014030c2dc  ret      
000000014030c2dd  lea      rax, [rsp + 0x4e]
000000014030c2e2  mov      qword ptr [rsp + 0x78], rax
000000014030c2e7  lea      rax, [rip - 0x2f367e] ; [0x140018c70] 
000000014030c2ee  mov      qword ptr [rsp + 0x80], rax
000000014030c2f6  lea      rax, [rsp + 0x50]
000000014030c2fb  mov      qword ptr [rsp + 0x88], rax
000000014030c303  lea      rax, [rip - 0x2f2eda] ; [0x140019430] 
000000014030c30a  mov      qword ptr [rsp + 0x90], rax
000000014030c312  lea      rdx, [rip + 0x3b1cf6] ; [0x1406be00f] rust-format-first-literal='raw_driver_read_ec('
000000014030c319  lea      rcx, [rsp + 0x58]
000000014030c31e  lea      r8, [rsp + 0x78]
000000014030c323  call     0x140003d70 ; 
000000014030c328  mov      rbx, qword ptr [rsp + 0x60]
000000014030c32d  mov      rax, qword ptr [rsp + 0x68]
000000014030c332  mov      r14, qword ptr [rsp + 0x58]
000000014030c337  mov      qword ptr [rsp + 0x28], rax
000000014030c33c  mov      qword ptr [rsp + 0x20], rbx
000000014030c341  lea      rcx, [rip + 0x352211] ; [0x14065e559] 
000000014030c348  lea      r8, [rip + 0x3b1a11] ; [0x1406bdd60] 
000000014030c34f  mov      edx, 5
000000014030c354  mov      r9d, 9
000000014030c35a  call     0x1403409f0 ; 
000000014030c35f  test     r14, r14
000000014030c362  je       0x14030c2b8
000000014030c368  call     qword ptr [rip + 0x2ea28a] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030c36e  mov      rcx, rax
000000014030c371  xor      edx, edx
000000014030c373  mov      r8, rbx
000000014030c376  call     qword ptr [rip + 0x2ea274] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014030c37c  jmp      0x14030c2b8 ; 
000000014030c381  mov      ecx, 1
000000014030c386  mov      edx, 0xf
000000014030c38b  call     0x1405cb7af ; 
000000014030c390  ud2      
