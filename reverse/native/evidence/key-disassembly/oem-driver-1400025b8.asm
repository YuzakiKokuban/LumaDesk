; oem-driver.sys SHA256=6e360ee4a0c42b2a4eaf46208c1592c87f8cd71aca2a6c52831a2ad356e61235
; preferred image VA 0x1400025b8..0x140002819
00000001400025b8  mov      qword ptr [rsp + 0x18], r8
00000001400025bd  mov      qword ptr [rsp + 0x10], rdx
00000001400025c2  mov      qword ptr [rsp + 8], rcx
00000001400025c7  sub      rsp, 0xd8
00000001400025ce  mov      rax, qword ptr [rip + 0x3a6b] ; [0x140006040] 
00000001400025d5  xor      rax, rsp
00000001400025d8  mov      qword ptr [rsp + 0xc8], rax
00000001400025e0  mov      dword ptr [rsp + 0x94], 0x57524345
00000001400025eb  mov      dword ptr [rsp + 0x90], 0x43696541
00000001400025f6  mov      dword ptr [rsp + 0x9c], 2
0000000140002601  mov      dword ptr [rsp + 0x98], 0x18
000000014000260c  mov      eax, dword ptr [rsp + 0x98]
0000000140002613  add      rax, 0x10
0000000140002617  mov      dword ptr [rsp + 0x98], eax
000000014000261e  mov      eax, 8
0000000140002623  imul     rax, rax, 0
0000000140002627  xor      ecx, ecx
0000000140002629  mov      word ptr [rsp + rax + 0xa0], cx
0000000140002631  mov      eax, 8
0000000140002636  imul     rax, rax, 0
000000014000263a  mov      ecx, 4
000000014000263f  mov      word ptr [rsp + rax + 0xa2], cx
0000000140002647  lea      rax, [rsp + 0xa8]
000000014000264f  mov      qword ptr [rsp + 0x48], rax
0000000140002654  xor      eax, eax
0000000140002656  mov      rcx, qword ptr [rsp + 0x48]
000000014000265b  mov      word ptr [rcx], ax
000000014000265e  mov      eax, 4
0000000140002663  mov      rcx, qword ptr [rsp + 0x48]
0000000140002668  mov      word ptr [rcx + 2], ax
000000014000266c  mov      eax, 8
0000000140002671  imul     rax, rax, 0
0000000140002675  movzx    eax, word ptr [rsp + rax + 0xa2]
000000014000267d  mov      ecx, 8
0000000140002682  imul     rcx, rcx, 0
0000000140002686  lea      rcx, [rsp + rcx + 0xa4]
000000014000268e  mov      r9d, eax
0000000140002691  xor      r8d, r8d
0000000140002694  mov      rdx, qword ptr [rsp + 0xe8]
000000014000269c  call     0x14000351c ; 
00000001400026a1  mov      eax, 8
00000001400026a6  imul     rax, rax, 0
00000001400026aa  movzx    eax, word ptr [rsp + rax + 0xa2]
00000001400026b2  mov      rcx, qword ptr [rsp + 0x48]
00000001400026b7  add      rcx, 4
00000001400026bb  mov      r9d, 1
00000001400026c1  mov      r8d, eax
00000001400026c4  mov      rdx, qword ptr [rsp + 0xe8]
00000001400026cc  call     0x14000351c ; 
00000001400026d1  mov      r8d, 0x28
00000001400026d7  lea      rdx, [rsp + 0x90]
00000001400026df  lea      rcx, [rsp + 0x78]
00000001400026e4  call     0x140003160 ; 
00000001400026e9  mov      r8d, 0x14
00000001400026ef  xor      edx, edx
00000001400026f1  lea      rcx, [rsp + 0xb0]
00000001400026f9  call     0x140003dc0 ; 
00000001400026fe  mov      r8d, 0x14
0000000140002704  lea      rdx, [rsp + 0xb0]
000000014000270c  lea      rcx, [rsp + 0x60]
0000000140002711  call     0x140003160 ; 
0000000140002716  mov      rcx, qword ptr [rsp + 0xe0]
000000014000271e  call     0x1400031b0 ; 
0000000140002723  mov      qword ptr [rsp + 0x58], rax
0000000140002728  lea      rax, [rsp + 0x50]
000000014000272d  mov      qword ptr [rsp + 0x30], rax
0000000140002732  mov      qword ptr [rsp + 0x28], 0
000000014000273b  lea      rax, [rsp + 0x60]
0000000140002740  mov      qword ptr [rsp + 0x20], rax
0000000140002745  lea      r9, [rsp + 0x78]
000000014000274a  mov      r8d, 0x32c004
0000000140002750  xor      edx, edx
0000000140002752  mov      rcx, qword ptr [rsp + 0x58]
0000000140002757  call     0x140003298 ; 
000000014000275c  mov      dword ptr [rsp + 0x44], eax
0000000140002760  cmp      dword ptr [rsp + 0x44], 0
0000000140002765  jge      0x140002771
0000000140002767  jmp      0x1400027fd ; 
000000014000276c  jmp      0x1400027fd ; 
0000000140002771  cmp      dword ptr [rsp + 0xb0], 0x426f6541
000000014000277c  jne      0x1400027f5
000000014000277e  mov      eax, 8
0000000140002783  imul     rax, rax, 0
0000000140002787  movzx    eax, word ptr [rsp + rax + 0xbc]
000000014000278f  test     eax, eax
0000000140002791  jne      0x1400027f5
0000000140002793  mov      eax, 8
0000000140002798  imul     rax, rax, 0
000000014000279c  movzx    eax, word ptr [rsp + rax + 0xbe]
00000001400027a4  mov      word ptr [rsp + 0x40], ax
00000001400027a9  movzx    eax, word ptr [rsp + 0x40]
00000001400027ae  mov      ecx, 8
00000001400027b3  imul     rcx, rcx, 0
00000001400027b7  lea      rcx, [rsp + rcx + 0xc0]
00000001400027bf  movzx    edx, word ptr [rsp + 0x40]
00000001400027c4  mov      r9d, eax
00000001400027c7  mov      r8, rcx
00000001400027ca  mov      rcx, qword ptr [rsp + 0xf0]
00000001400027d2  call     0x140003640 ; 
00000001400027d7  movzx    eax, word ptr [rsp + 0x40]
00000001400027dc  mov      edx, eax
00000001400027de  mov      rcx, qword ptr [rsp + 0xf0]
00000001400027e6  call     0x14000281c ; 
00000001400027eb  mov      dword ptr [rsp + 0x44], 0
00000001400027f3  jmp      0x1400027fd ; 
00000001400027f5  mov      dword ptr [rsp + 0x44], 0xc014000f
00000001400027fd  mov      eax, dword ptr [rsp + 0x44]
0000000140002801  mov      rcx, qword ptr [rsp + 0xc8]
0000000140002809  xor      rcx, rsp
000000014000280c  call     0x140003580 ; 
0000000140002811  add      rsp, 0xd8
0000000140002818  ret      
