; oem-acpi.dll SHA256=97d7115943600c2a09951440859f9bd75fd0d8bff9db49c296c868b49df8c8c6
; preferred image VA 0x1800034f0..0x1800035ca
00000001800034f0  push     rbx
00000001800034f2  sub      rsp, 0x50
00000001800034f6  mov      rax, qword ptr [rip + 0x26c3e3] ; [0x18026f8e0] 
00000001800034fd  xor      rax, rsp
0000000180003500  mov      qword ptr [rsp + 0x48], rax
0000000180003505  xor      r9d, r9d
0000000180003508  mov      dword ptr [rip + 0x279792], ecx ; [0x18027cca0] 
000000018000350e  mov      dword ptr [rip + 0x279790], edx ; [0x18027cca4] 
0000000180003514  lea      rcx, [rip + 0x22f4a5] ; [0x1802329c0] 
000000018000351b  mov      qword ptr [rsp + 0x30], 0
0000000180003524  mov      edx, 0xc0000000
0000000180003529  mov      dword ptr [rsp + 0x28], 0
0000000180003531  lea      r8d, [r9 + 3]
0000000180003535  mov      dword ptr [rsp + 0x20], 3
000000018000353d  call     qword ptr [rip + 0x1c82fd] ; [0x1801cb840] KERNEL32.dll!CreateFileW
0000000180003543  mov      rbx, rax
0000000180003546  cmp      rax, -1
000000018000354a  jne      0x180003573
000000018000354c  xor      r9d, r9d
000000018000354f  lea      r8, [rip + 0x22f48a] ; [0x1802329e0] 
0000000180003556  xor      edx, edx
0000000180003558  xor      ecx, ecx
000000018000355a  call     qword ptr [rip + 0x1c8968] ; [0x1801cbec8] USER32.dll!MessageBoxW
0000000180003560  mov      rcx, qword ptr [rsp + 0x48]
0000000180003565  xor      rcx, rsp
0000000180003568  call     0x1801a1740 ; 
000000018000356d  add      rsp, 0x50
0000000180003571  pop      rbx
0000000180003572  ret      
0000000180003573  mov      qword ptr [rsp + 0x38], 0
000000018000357c  lea      rax, [rsp + 0x40]
0000000180003581  mov      qword ptr [rsp + 0x30], rax
0000000180003586  lea      r8, [rip + 0x279713] ; [0x18027cca0] 
000000018000358d  mov      dword ptr [rsp + 0x28], 0x400000
0000000180003595  mov      edx, 0x9c40a48c
000000018000359a  mov      r9d, 0x400000
00000001800035a0  mov      qword ptr [rsp + 0x20], r8
00000001800035a5  mov      rcx, rbx
00000001800035a8  call     qword ptr [rip + 0x1c828a] ; [0x1801cb838] KERNEL32.dll!DeviceIoControl
00000001800035ae  mov      rcx, rbx
00000001800035b1  call     qword ptr [rip + 0x1c8279] ; [0x1801cb830] KERNEL32.dll!CloseHandle
00000001800035b7  mov      rcx, qword ptr [rsp + 0x48]
00000001800035bc  xor      rcx, rsp
00000001800035bf  call     0x1801a1740 ; 
00000001800035c4  add      rsp, 0x50
00000001800035c8  pop      rbx
00000001800035c9  ret      
