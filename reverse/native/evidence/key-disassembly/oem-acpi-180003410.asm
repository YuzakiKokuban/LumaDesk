; oem-acpi.dll SHA256=97d7115943600c2a09951440859f9bd75fd0d8bff9db49c296c868b49df8c8c6
; preferred image VA 0x180003410..0x1800034ed
0000000180003410  push     rbx
0000000180003412  sub      rsp, 0x50
0000000180003416  mov      rax, qword ptr [rip + 0x26c4c3] ; [0x18026f8e0] 
000000018000341d  xor      rax, rsp
0000000180003420  mov      qword ptr [rsp + 0x48], rax
0000000180003425  xor      r9d, r9d
0000000180003428  mov      dword ptr [rip + 0x279872], ecx ; [0x18027cca0] 
000000018000342e  mov      qword ptr [rsp + 0x30], 0
0000000180003437  lea      rcx, [rip + 0x22f582] ; [0x1802329c0] 
000000018000343e  mov      dword ptr [rsp + 0x28], 0
0000000180003446  mov      edx, 0xc0000000
000000018000344b  mov      dword ptr [rsp + 0x20], 3
0000000180003453  lea      r8d, [r9 + 3]
0000000180003457  call     qword ptr [rip + 0x1c83e3] ; [0x1801cb840] KERNEL32.dll!CreateFileW
000000018000345d  mov      rbx, rax
0000000180003460  cmp      rax, -1
0000000180003464  jne      0x18000348f
0000000180003466  xor      r9d, r9d
0000000180003469  lea      r8, [rip + 0x22f570] ; [0x1802329e0] 
0000000180003470  xor      edx, edx
0000000180003472  xor      ecx, ecx
0000000180003474  call     qword ptr [rip + 0x1c8a4e] ; [0x1801cbec8] USER32.dll!MessageBoxW
000000018000347a  xor      eax, eax
000000018000347c  mov      rcx, qword ptr [rsp + 0x48]
0000000180003481  xor      rcx, rsp
0000000180003484  call     0x1801a1740 ; 
0000000180003489  add      rsp, 0x50
000000018000348d  pop      rbx
000000018000348e  ret      
000000018000348f  mov      qword ptr [rsp + 0x38], 0
0000000180003498  lea      rax, [rsp + 0x40]
000000018000349d  mov      qword ptr [rsp + 0x30], rax
00000001800034a2  lea      r8, [rip + 0x2797f7] ; [0x18027cca0] 
00000001800034a9  mov      dword ptr [rsp + 0x28], 0x400000
00000001800034b1  mov      edx, 0x9c40a488
00000001800034b6  mov      r9d, 0x400000
00000001800034bc  mov      qword ptr [rsp + 0x20], r8
00000001800034c1  mov      rcx, rbx
00000001800034c4  call     qword ptr [rip + 0x1c836e] ; [0x1801cb838] KERNEL32.dll!DeviceIoControl
00000001800034ca  mov      rcx, rbx
00000001800034cd  call     qword ptr [rip + 0x1c835d] ; [0x1801cb830] KERNEL32.dll!CloseHandle
00000001800034d3  movzx    eax, byte ptr [rip + 0x2797c6] ; [0x18027cca0] 
00000001800034da  mov      rcx, qword ptr [rsp + 0x48]
00000001800034df  xor      rcx, rsp
00000001800034e2  call     0x1801a1740 ; 
00000001800034e7  add      rsp, 0x50
00000001800034eb  pop      rbx
00000001800034ec  ret      
