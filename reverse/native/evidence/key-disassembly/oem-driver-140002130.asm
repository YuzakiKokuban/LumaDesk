; oem-driver.sys SHA256=6e360ee4a0c42b2a4eaf46208c1592c87f8cd71aca2a6c52831a2ad356e61235
; preferred image VA 0x140002130..0x14000236f
0000000140002130  mov      qword ptr [rsp + 0x18], r8
0000000140002135  mov      qword ptr [rsp + 0x10], rdx
000000014000213a  mov      qword ptr [rsp + 8], rcx
000000014000213f  sub      rsp, 0xd8
0000000140002146  mov      rax, qword ptr [rip + 0x3ef3] ; [0x140006040] 
000000014000214d  xor      rax, rsp
0000000140002150  mov      qword ptr [rsp + 0xc0], rax
0000000140002158  mov      dword ptr [rsp + 0x94], 0x52524345
0000000140002163  mov      dword ptr [rsp + 0x90], 0x43696541
000000014000216e  mov      dword ptr [rsp + 0x9c], 1
0000000140002179  mov      dword ptr [rsp + 0x98], 0x18
0000000140002184  mov      eax, dword ptr [rsp + 0x98]
000000014000218b  add      rax, 0x10
000000014000218f  mov      dword ptr [rsp + 0x98], eax
0000000140002196  mov      eax, 8
000000014000219b  imul     rax, rax, 0
000000014000219f  xor      ecx, ecx
00000001400021a1  mov      word ptr [rsp + rax + 0xa0], cx
00000001400021a9  mov      eax, 8
00000001400021ae  imul     rax, rax, 0
00000001400021b2  mov      ecx, 4
00000001400021b7  mov      word ptr [rsp + rax + 0xa2], cx
00000001400021bf  mov      eax, 8
00000001400021c4  imul     rax, rax, 0
00000001400021c8  movzx    eax, word ptr [rsp + rax + 0xa2]
00000001400021d0  mov      word ptr [rsp + 0x40], ax
00000001400021d5  mov      eax, 8
00000001400021da  imul     rax, rax, 0
00000001400021de  movzx    eax, word ptr [rsp + rax + 0xa2]
00000001400021e6  mov      ecx, 8
00000001400021eb  imul     rcx, rcx, 0
00000001400021ef  movzx    ecx, word ptr [rsp + rcx + 0xa2]
00000001400021f7  mov      edx, 8
00000001400021fc  imul     rdx, rdx, 0
0000000140002200  lea      rdx, [rsp + rdx + 0xa4]
0000000140002208  mov      qword ptr [rsp + 0x50], rdx
000000014000220d  mov      r9d, eax
0000000140002210  mov      r8, qword ptr [rsp + 0xe8]
0000000140002218  mov      edx, ecx
000000014000221a  mov      rax, qword ptr [rsp + 0x50]
000000014000221f  mov      rcx, rax
0000000140002222  call     0x140003640 ; 
0000000140002227  mov      r8d, 0x28
000000014000222d  lea      rdx, [rsp + 0x90]
0000000140002235  lea      rcx, [rsp + 0x78]
000000014000223a  call     0x140003160 ; 
000000014000223f  mov      r8d, 0x14
0000000140002245  xor      edx, edx
0000000140002247  lea      rcx, [rsp + 0xa8]
000000014000224f  call     0x140003dc0 ; 
0000000140002254  mov      r8d, 0x14
000000014000225a  lea      rdx, [rsp + 0xa8]
0000000140002262  lea      rcx, [rsp + 0x60]
0000000140002267  call     0x140003160 ; 
000000014000226c  mov      rcx, qword ptr [rsp + 0xe0]
0000000140002274  call     0x1400031b0 ; 
0000000140002279  mov      qword ptr [rsp + 0x58], rax
000000014000227e  lea      rax, [rsp + 0x48]
0000000140002283  mov      qword ptr [rsp + 0x30], rax
0000000140002288  mov      qword ptr [rsp + 0x28], 0
0000000140002291  lea      rax, [rsp + 0x60]
0000000140002296  mov      qword ptr [rsp + 0x20], rax
000000014000229b  lea      r9, [rsp + 0x78]
00000001400022a0  mov      r8d, 0x32c004
00000001400022a6  xor      edx, edx
00000001400022a8  mov      rcx, qword ptr [rsp + 0x58]
00000001400022ad  call     0x140003298 ; 
00000001400022b2  mov      dword ptr [rsp + 0x44], eax
00000001400022b6  cmp      dword ptr [rsp + 0x44], 0
00000001400022bb  jge      0x1400022c7
00000001400022bd  jmp      0x140002353 ; 
00000001400022c2  jmp      0x140002353 ; 
00000001400022c7  cmp      dword ptr [rsp + 0xa8], 0x426f6541
00000001400022d2  jne      0x14000234b
00000001400022d4  mov      eax, 8
00000001400022d9  imul     rax, rax, 0
00000001400022dd  movzx    eax, word ptr [rsp + rax + 0xb4]
00000001400022e5  test     eax, eax
00000001400022e7  jne      0x14000234b
00000001400022e9  mov      eax, 8
00000001400022ee  imul     rax, rax, 0
00000001400022f2  movzx    eax, word ptr [rsp + rax + 0xb6]
00000001400022fa  mov      word ptr [rsp + 0x40], ax
00000001400022ff  movzx    eax, word ptr [rsp + 0x40]
0000000140002304  mov      ecx, 8
0000000140002309  imul     rcx, rcx, 0
000000014000230d  lea      rcx, [rsp + rcx + 0xb8]
0000000140002315  movzx    edx, word ptr [rsp + 0x40]
000000014000231a  mov      r9d, eax
000000014000231d  mov      r8, rcx
0000000140002320  mov      rcx, qword ptr [rsp + 0xf0]
0000000140002328  call     0x140003640 ; 
000000014000232d  movzx    eax, word ptr [rsp + 0x40]
0000000140002332  mov      edx, eax
0000000140002334  mov      rcx, qword ptr [rsp + 0xf0]
000000014000233c  call     0x14000281c ; 
0000000140002341  mov      dword ptr [rsp + 0x44], 0
0000000140002349  jmp      0x140002353 ; 
000000014000234b  mov      dword ptr [rsp + 0x44], 0xc014000f
0000000140002353  mov      eax, dword ptr [rsp + 0x44]
0000000140002357  mov      rcx, qword ptr [rsp + 0xc0]
000000014000235f  xor      rcx, rsp
0000000140002362  call     0x140003580 ; 
0000000140002367  add      rsp, 0xd8
000000014000236e  ret      
