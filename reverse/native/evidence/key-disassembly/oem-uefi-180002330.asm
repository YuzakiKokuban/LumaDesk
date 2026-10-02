; oem-uefi.dll SHA256=4cfe064827dad6da7bf6fb9fc07dc00b4032f1881331d59bfa8d5937a6d534be
; preferred image VA 0x180002330..0x1800024f0
0000000180002330  mov      qword ptr [rsp + 0x18], rbx
0000000180002335  mov      qword ptr [rsp + 0x20], rbp
000000018000233a  push     rdi
000000018000233b  sub      rsp, 0x220
0000000180002342  mov      rdi, r8
0000000180002345  movsxd   rbp, edx
0000000180002348  movsxd   rbx, ecx
000000018000234b  call     0x180002f10 ; 
0000000180002350  test     eax, eax
0000000180002352  je       0x1800024d5
0000000180002358  mov      qword ptr [rsp + 0x230], rsi
0000000180002360  lea      rcx, [rip + 0x29b09] ; [0x18002be70] 
0000000180002367  xor      edx, edx
0000000180002369  mov      qword ptr [rsp + 0x238], r14
0000000180002371  mov      r8d, 0x200
0000000180002377  call     0x18001b550 ; 
000000018000237c  mov      r9d, 0x200
0000000180002382  lea      r8, [rip + 0x29ae7] ; [0x18002be70] 
0000000180002389  lea      rdx, [rip + 0x23e40] ; [0x1800261d0] 
0000000180002390  lea      rcx, [rip + 0x23e89] ; [0x180026220] 
0000000180002397  call     qword ptr [rip + 0x1acf3] ; [0x18001d090] KERNEL32.dll!GetFirmwareEnvironmentVariableW
000000018000239d  mov      ecx, 0x200
00000001800023a2  mov      r14d, eax
00000001800023a5  call     0x1800037c8 ; 
00000001800023aa  mov      rsi, rax
00000001800023ad  mov      rcx, rax
00000001800023b0  xorps    xmm0, xmm0
00000001800023b3  mov      edx, 4
00000001800023b8  nop      dword ptr [rax + rax]
00000001800023c0  movups   xmmword ptr [rcx], xmm0
00000001800023c3  movups   xmmword ptr [rcx + 0x10], xmm0
00000001800023c7  movups   xmmword ptr [rcx + 0x20], xmm0
00000001800023cb  movups   xmmword ptr [rcx + 0x30], xmm0
00000001800023cf  movups   xmmword ptr [rcx + 0x40], xmm0
00000001800023d3  movups   xmmword ptr [rcx + 0x50], xmm0
00000001800023d7  movups   xmmword ptr [rcx + 0x60], xmm0
00000001800023db  lea      rcx, [rcx + 0x80]
00000001800023e2  movups   xmmword ptr [rcx - 0x10], xmm0
00000001800023e6  sub      rdx, 1
00000001800023ea  jne      0x1800023c0
00000001800023ec  mov      eax, ebp
00000001800023ee  sub      eax, ebx
00000001800023f0  add      eax, 1
00000001800023f3  movsxd   r8, eax
00000001800023f6  je       0x180002415
00000001800023f8  nop      dword ptr [rax + rax]
0000000180002400  lea      eax, [rdx + rbx]
0000000180002403  movsxd   rcx, eax
0000000180002406  movzx    eax, byte ptr [rdi + rdx]
000000018000240a  inc      rdx
000000018000240d  mov      byte ptr [rcx + rsi], al
0000000180002410  cmp      rdx, r8
0000000180002413  jb       0x180002400
0000000180002415  mov      rdi, rbx
0000000180002418  lea      r8, [rsp + 0x20]
000000018000241d  test     r14d, r14d
0000000180002420  mov      rbx, rbp
0000000180002423  mov      r14, qword ptr [rsp + 0x238]
000000018000242b  mov      r9d, 0x200
0000000180002431  jle      0x180002476
0000000180002433  lea      rdx, [rip + 0x23d96] ; [0x1800261d0] 
000000018000243a  lea      rcx, [rip + 0x23ddf] ; [0x180026220] 
0000000180002441  call     qword ptr [rip + 0x1ac49] ; [0x18001d090] KERNEL32.dll!GetFirmwareEnvironmentVariableW
0000000180002447  mov      ebp, eax
0000000180002449  cmp      rdi, rbx
000000018000244c  ja       0x180002466
000000018000244e  sub      rbx, rdi
0000000180002451  lea      rcx, [rsp + 0x20]
0000000180002456  add      rcx, rdi
0000000180002459  lea      rdx, [rdi + rsi]
000000018000245d  lea      r8, [rbx + 1]
0000000180002461  call     0x18001aeb0 ; 
0000000180002466  lea      rdx, [rip + 0x23d63] ; [0x1800261d0] 
000000018000246d  lea      rcx, [rip + 0x23dac] ; [0x180026220] 
0000000180002474  jmp      0x1800024b7 ; 
0000000180002476  lea      rdx, [rip + 0x23dd3] ; [0x180026250] 
000000018000247d  lea      rcx, [rip + 0x23e1c] ; [0x1800262a0] 
0000000180002484  call     qword ptr [rip + 0x1ac06] ; [0x18001d090] KERNEL32.dll!GetFirmwareEnvironmentVariableW
000000018000248a  mov      ebp, eax
000000018000248c  cmp      rdi, rbx
000000018000248f  ja       0x1800024a9
0000000180002491  sub      rbx, rdi
0000000180002494  lea      rcx, [rsp + 0x20]
0000000180002499  add      rcx, rdi
000000018000249c  lea      rdx, [rdi + rsi]
00000001800024a0  lea      r8, [rbx + 1]
00000001800024a4  call     0x18001aeb0 ; 
00000001800024a9  lea      rdx, [rip + 0x23da0] ; [0x180026250] 
00000001800024b0  lea      rcx, [rip + 0x23de9] ; [0x1800262a0] 
00000001800024b7  mov      r9d, ebp
00000001800024ba  lea      r8, [rsp + 0x20]
00000001800024bf  call     qword ptr [rip + 0x1ac43] ; [0x18001d108] KERNEL32.dll!SetFirmwareEnvironmentVariableW
00000001800024c5  mov      rcx, rsi
00000001800024c8  call     0x18000e3a0 ; 
00000001800024cd  mov      rsi, qword ptr [rsp + 0x230]
00000001800024d5  lea      r11, [rsp + 0x220]
00000001800024dd  mov      rbx, qword ptr [r11 + 0x20]
00000001800024e1  mov      rbp, qword ptr [r11 + 0x28]
00000001800024e5  mov      rsp, r11
00000001800024e8  pop      rdi
00000001800024e9  ret      
00000001800024ea  int3     
00000001800024eb  int3     
00000001800024ec  int3     
00000001800024ed  int3     
00000001800024ee  int3     
00000001800024ef  int3     
