; openrevo.exe SHA256=3e3b9d2297e36a0361d56c0f6357afdd9ebc8b201b943546c5cf8002cfabda09
; preferred image VA 0x140307500..0x140307c71
0000000140307500  push     r15
0000000140307502  push     r14
0000000140307504  push     r12
0000000140307506  push     rsi
0000000140307507  push     rdi
0000000140307508  push     rbp
0000000140307509  push     rbx
000000014030750a  sub      rsp, 0x1c0
0000000140307511  mov      rdi, r8
0000000140307514  mov      rbx, rdx
0000000140307517  mov      rsi, rcx
000000014030751a  mov      qword ptr [rsp + 0x190], rdx
0000000140307522  mov      qword ptr [rsp + 0x198], r8
000000014030752a  lea      rcx, [rsp + 0x110]
0000000140307532  call     0x140306a50 ; 
0000000140307537  mov      rax, qword ptr [rsp + 0x120]
000000014030753f  mov      qword ptr [rsp + 0xf0], rax
0000000140307547  movups   xmm0, xmmword ptr [rsp + 0x110]
000000014030754f  movaps   xmmword ptr [rsp + 0xe0], xmm0
0000000140307557  movzx    ebp, byte ptr [rsp + 0x128]
000000014030755f  lea      rcx, [rsp + 0x178]
0000000140307567  mov      rdx, rbx
000000014030756a  mov      r8, rdi
000000014030756d  call     0x140001bd0 ; 
0000000140307572  lea      rcx, [rsp + 0x110]
000000014030757a  call     0x1403088f0 ; 
000000014030757f  mov      rdi, qword ptr [rsp + 0x180]
0000000140307587  mov      rbx, qword ptr [rsp + 0x188]
000000014030758f  cmp      rbx, 4
0000000140307593  jne      0x1403075d0
0000000140307595  cmp      dword ptr [rdi], 0x75706769
000000014030759b  jne      0x1403075d0
000000014030759d  cmp      byte ptr [rsp + 0x172], 0
00000001403075a5  jne      0x1403075d0
00000001403075a7  mov      rax, qword ptr [rsp + 0x158]
00000001403075af  cmp      rax, -1
00000001403075b3  je       0x1403076fa
00000001403075b9  mov      qword ptr [rsp + 0x40], rax
00000001403075be  movups   xmm0, xmmword ptr [rsp + 0x160]
00000001403075c6  movups   xmmword ptr [rsp + 0x48], xmm0
00000001403075cb  jmp      0x140307752 ; 
00000001403075d0  test     bpl, bpl
00000001403075d3  je       0x1403075fd
00000001403075d5  cmp      rbx, 4
00000001403075d9  je       0x14030762a
00000001403075db  cmp      rbx, 6
00000001403075df  jne      0x140307642
00000001403075e1  mov      eax, 0x72627968
00000001403075e6  xor      eax, dword ptr [rdi]
00000001403075e8  movzx    ecx, word ptr [rdi + 4]
00000001403075ec  xor      ecx, 0x6469
00000001403075f2  or       ecx, eax
00000001403075f4  jne      0x140307642
00000001403075f6  xor      ebp, ebp
00000001403075f8  jmp      0x140307902 ; 
00000001403075fd  cmp      rbx, 4
0000000140307601  je       0x140307675
0000000140307603  cmp      rbx, 6
0000000140307607  jne      0x14030768d
000000014030760d  mov      eax, 0x72627968
0000000140307612  xor      eax, dword ptr [rdi]
0000000140307614  movzx    ecx, word ptr [rdi + 4]
0000000140307618  xor      ecx, 0x6469
000000014030761e  or       ecx, eax
0000000140307620  jne      0x14030768d
0000000140307622  mov      bpl, 4
0000000140307625  jmp      0x140307902 ; 
000000014030762a  cmp      dword ptr [rdi], 0x75706764
0000000140307630  je       0x1403078ff
0000000140307636  cmp      dword ptr [rdi], 0x75706769
000000014030763c  je       0x1403078fa
0000000140307642  mov      qword ptr [rsp + 0x40], rdi
0000000140307647  mov      qword ptr [rsp + 0x48], rbx
000000014030764c  lea      rax, [rsp + 0x40]
0000000140307651  mov      qword ptr [rsp + 0x90], rax
0000000140307659  lea      rax, [rip - 0x2eacc0] ; [0x14001c9a0] 
0000000140307660  mov      qword ptr [rsp + 0x98], rax
0000000140307668  lea      rcx, [rsi + 8]
000000014030766c  lea      rdx, [rip + 0x3b4a63] ; [0x1406bc0d6] rust-format-first-literal="Unknown GPU mode '"
0000000140307673  jmp      0x1403076be ; 
0000000140307675  cmp      dword ptr [rdi], 0x75706764
000000014030767b  je       0x1403078fa
0000000140307681  cmp      dword ptr [rdi], 0x75706769
0000000140307687  je       0x1403078ff
000000014030768d  mov      qword ptr [rsp + 0x40], rdi
0000000140307692  mov      qword ptr [rsp + 0x48], rbx
0000000140307697  lea      rax, [rsp + 0x40]
000000014030769c  mov      qword ptr [rsp + 0x90], rax
00000001403076a4  lea      rax, [rip - 0x2ead0b] ; [0x14001c9a0] 
00000001403076ab  mov      qword ptr [rsp + 0x98], rax
00000001403076b3  lea      rcx, [rsi + 8]
00000001403076b7  lea      rdx, [rip + 0x3b49ee] ; [0x1406bc0ac] rust-format-first-literal="Unknown GPU mode '"
00000001403076be  lea      r8, [rsp + 0x90]
00000001403076c6  call     0x140003d70 ; 
00000001403076cb  mov      qword ptr [rsi], 0xffffffffffffffff
00000001403076d2  mov      rax, qword ptr [rsp + 0x158]
00000001403076da  cmp      rax, -1
00000001403076de  je       0x14030782a
00000001403076e4  test     rax, rax
00000001403076e7  je       0x14030782a
00000001403076ed  mov      rbx, qword ptr [rsp + 0x160]
00000001403076f5  jmp      0x140307816 ; 
00000001403076fa  mov      r8d, 0x3c
0000000140307700  xor      edx, edx
0000000140307702  call     0x14050b9d0 ; 
0000000140307707  test     rax, rax
000000014030770a  je       0x140307c47
0000000140307710  movups   xmm0, xmmword ptr [rip + 0x356bf0] ; [0x14065e307] 
0000000140307717  movups   xmmword ptr [rax + 0x2c], xmm0
000000014030771b  movups   xmm0, xmmword ptr [rip + 0x356bd9] ; [0x14065e2fb] 
0000000140307722  movups   xmmword ptr [rax + 0x20], xmm0
0000000140307726  movups   xmm0, xmmword ptr [rip + 0x356bbe] ; [0x14065e2eb] 
000000014030772d  movups   xmmword ptr [rax + 0x10], xmm0
0000000140307731  movups   xmm0, xmmword ptr [rip + 0x356ba3] ; [0x14065e2db] 
0000000140307738  movups   xmmword ptr [rax], xmm0
000000014030773b  mov      qword ptr [rsp + 0x40], 0x3c
0000000140307744  mov      qword ptr [rsp + 0x48], rax
0000000140307749  mov      qword ptr [rsp + 0x50], 0x3c
0000000140307752  lea      r14, [rsp + 0x40]
0000000140307757  mov      qword ptr [rsp + 0x70], r14
000000014030775c  lea      r15, [rip - 0x302613] ; [0x140005150] 
0000000140307763  mov      qword ptr [rsp + 0x78], r15
0000000140307768  lea      rdx, [rip + 0x3b48aa] ; [0x1406bc019] rust-format-first-literal='Anti-brick safety guard intercepted illegal iGPU write: '
000000014030776f  lea      rcx, [rsp + 0x90]
0000000140307777  lea      r8, [rsp + 0x70]
000000014030777c  call     0x140003d70 ; 
0000000140307781  mov      rbx, qword ptr [rsp + 0x98]
0000000140307789  mov      rax, qword ptr [rsp + 0xa0]
0000000140307791  mov      r12, qword ptr [rsp + 0x90]
0000000140307799  mov      qword ptr [rsp + 0x28], rax
000000014030779e  mov      qword ptr [rsp + 0x20], rbx
00000001403077a3  lea      rcx, [rip + 0x3546d2] ; [0x14065be7c] 'ERRORWINDOWexport_device_diagnostic_reportgpuStateset_battery_modemodeecStateget_power_settingsset_cpu_safety_guardset_cpu_freq_limitmhzset_gpu_moderestartget_gpu_mode_infoset_device_switchswi'
00000001403077aa  lea      r8, [rip + 0x3b48a3] ; [0x1406bc054] 
00000001403077b1  mov      edx, 5
00000001403077b6  mov      r9d, 9
00000001403077bc  call     0x1403409f0 ; 
00000001403077c1  test     r12, r12
00000001403077c4  je       0x1403077da
00000001403077c6  call     qword ptr [rip + 0x2eee2c] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
00000001403077cc  mov      rcx, rax
00000001403077cf  xor      edx, edx
00000001403077d1  mov      r8, rbx
00000001403077d4  call     qword ptr [rip + 0x2eee16] ; [0x1405f65f0] kernel32.dll!HeapFree
00000001403077da  mov      qword ptr [rsp + 0x90], r14
00000001403077e2  mov      qword ptr [rsp + 0x98], r15
00000001403077ea  lea      rcx, [rsi + 8]
00000001403077ee  lea      rdx, [rip + 0x3b4868] ; [0x1406bc05d] 
00000001403077f5  lea      r8, [rsp + 0x90]
00000001403077fd  call     0x140003d70 ; 
0000000140307802  mov      qword ptr [rsi], 0xffffffffffffffff
0000000140307809  cmp      qword ptr [rsp + 0x40], 0
000000014030780f  je       0x14030782a
0000000140307811  mov      rbx, qword ptr [rsp + 0x48]
0000000140307816  call     qword ptr [rip + 0x2eeddc] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030781c  mov      rcx, rax
000000014030781f  xor      edx, edx
0000000140307821  mov      r8, rbx
0000000140307824  call     qword ptr [rip + 0x2eedc6] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014030782a  cmp      qword ptr [rsp + 0x110], 0
0000000140307833  je       0x140307851
0000000140307835  mov      rbx, qword ptr [rsp + 0x118]
000000014030783d  call     qword ptr [rip + 0x2eedb5] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
0000000140307843  mov      rcx, rax
0000000140307846  xor      edx, edx
0000000140307848  mov      r8, rbx
000000014030784b  call     qword ptr [rip + 0x2eed9f] ; [0x1405f65f0] kernel32.dll!HeapFree
0000000140307851  cmp      qword ptr [rsp + 0x128], 0
000000014030785a  je       0x140307878
000000014030785c  mov      rbx, qword ptr [rsp + 0x130]
0000000140307864  call     qword ptr [rip + 0x2eed8e] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030786a  mov      rcx, rax
000000014030786d  xor      edx, edx
000000014030786f  mov      r8, rbx
0000000140307872  call     qword ptr [rip + 0x2eed78] ; [0x1405f65f0] kernel32.dll!HeapFree
0000000140307878  cmp      qword ptr [rsp + 0x140], 0
0000000140307881  je       0x14030789f
0000000140307883  mov      rbx, qword ptr [rsp + 0x148]
000000014030788b  call     qword ptr [rip + 0x2eed67] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
0000000140307891  mov      rcx, rax
0000000140307894  xor      edx, edx
0000000140307896  mov      r8, rbx
0000000140307899  call     qword ptr [rip + 0x2eed51] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014030789f  cmp      qword ptr [rsp + 0x178], 0
00000001403078a8  je       0x1403078be
00000001403078aa  call     qword ptr [rip + 0x2eed48] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
00000001403078b0  mov      rcx, rax
00000001403078b3  xor      edx, edx
00000001403078b5  mov      r8, rdi
00000001403078b8  call     qword ptr [rip + 0x2eed32] ; [0x1405f65f0] kernel32.dll!HeapFree
00000001403078be  cmp      qword ptr [rsp + 0xe0], 0
00000001403078c7  je       0x1403078e5
00000001403078c9  mov      rdi, qword ptr [rsp + 0xe8]
00000001403078d1  call     qword ptr [rip + 0x2eed21] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
00000001403078d7  mov      rcx, rax
00000001403078da  xor      edx, edx
00000001403078dc  mov      r8, rdi
00000001403078df  call     qword ptr [rip + 0x2eed0b] ; [0x1405f65f0] kernel32.dll!HeapFree
00000001403078e5  mov      rax, rsi
00000001403078e8  add      rsp, 0x1c0
00000001403078ef  pop      rbx
00000001403078f0  pop      rbp
00000001403078f1  pop      rdi
00000001403078f2  pop      rsi
00000001403078f3  pop      r12
00000001403078f5  pop      r14
00000001403078f7  pop      r15
00000001403078f9  ret      
00000001403078fa  mov      bpl, 2
00000001403078fd  jmp      0x140307902 ; 
00000001403078ff  mov      bpl, 1
0000000140307902  mov      byte ptr [rsp + 0x3f], bpl
0000000140307907  lea      rcx, [rsp + 0x90]
000000014030790f  call     0x140307e50 ; 
0000000140307914  mov      rax, qword ptr [rsp + 0x90]
000000014030791c  movups   xmm0, xmmword ptr [rsp + 0x98]
0000000140307924  movaps   xmmword ptr [rsp + 0x70], xmm0
0000000140307929  mov      rcx, qword ptr [rsp + 0xa8]
0000000140307931  mov      qword ptr [rsp + 0x80], rcx
0000000140307939  cmp      rax, -1
000000014030793d  je       0x1403079cd
0000000140307943  movups   xmm0, xmmword ptr [rsp + 0xb0]
000000014030794b  movups   xmmword ptr [rsp + 0x60], xmm0
0000000140307950  movaps   xmm0, xmmword ptr [rsp + 0x70]
0000000140307955  movups   xmmword ptr [rsp + 0x48], xmm0
000000014030795a  mov      rcx, qword ptr [rsp + 0x80]
0000000140307962  mov      qword ptr [rsp + 0x58], rcx
0000000140307967  mov      qword ptr [rsp + 0x40], rax
000000014030796c  lea      r14, [rsp + 0x68]
0000000140307971  mov      edx, dword ptr [rsp + 0x68]
0000000140307975  cmp      rdx, 0x63
0000000140307979  jae      0x1403079e7
000000014030797b  mov      qword ptr [rsp + 0x90], r14
0000000140307983  lea      rax, [rip - 0x2ee55a] ; [0x140019430] 
000000014030798a  mov      qword ptr [rsp + 0x98], rax
0000000140307992  lea      rax, [rip + 0x3b462f] ; [0x1406bbfc8] 
0000000140307999  mov      qword ptr [rsp + 0xa0], rax
00000001403079a1  lea      rax, [rip - 0x2ed888] ; [0x14001a120] 
00000001403079a8  mov      qword ptr [rsp + 0xa8], rax
00000001403079b0  lea      rcx, [rsi + 8]
00000001403079b4  lea      rdx, [rip + 0x3b4615] ; [0x1406bbfd0] rust-format-first-literal='NVRAM size ('
00000001403079bb  lea      r8, [rsp + 0x90]
00000001403079c3  call     0x140003d70 ; 
00000001403079c8  jmp      0x140307a55 ; 
00000001403079cd  mov      rax, qword ptr [rsp + 0x80]
00000001403079d5  mov      qword ptr [rsi + 0x18], rax
00000001403079d9  movaps   xmm0, xmmword ptr [rsp + 0x70]
00000001403079de  movups   xmmword ptr [rsi + 8], xmm0
00000001403079e2  jmp      0x1403076cb ; 
00000001403079e7  mov      r8, qword ptr [rsp + 0x50]
00000001403079ec  cmp      r8, 0x62
00000001403079f0  jbe      0x140307c58
00000001403079f6  mov      rcx, qword ptr [rsp + 0x48]
00000001403079fb  mov      byte ptr [rcx + 0x62], bpl
00000001403079ff  cmp      r8, rdx
0000000140307a02  jb       0x140307bd6
0000000140307a08  movzx    r9d, byte ptr [rsp + 0x6c]
0000000140307a0e  mov      rax, qword ptr [rsp + 0x58]
0000000140307a13  mov      r8, qword ptr [rsp + 0x60]
0000000140307a18  mov      qword ptr [rsp + 0x28], rdx
0000000140307a1d  mov      qword ptr [rsp + 0x20], rcx
0000000140307a22  lea      rcx, [rsp + 0x90]
0000000140307a2a  mov      rdx, rax
0000000140307a2d  call     0x140308360 ; 
0000000140307a32  cmp      qword ptr [rsp + 0x90], -1
0000000140307a3b  je       0x140307a86
0000000140307a3d  mov      rax, qword ptr [rsp + 0xa0]
0000000140307a45  mov      qword ptr [rsi + 0x18], rax
0000000140307a49  movups   xmm0, xmmword ptr [rsp + 0x90]
0000000140307a51  movups   xmmword ptr [rsi + 8], xmm0
0000000140307a55  mov      qword ptr [rsi], 0xffffffffffffffff
0000000140307a5c  cmp      qword ptr [rsp + 0x40], 0
0000000140307a62  je       0x1403076d2
0000000140307a68  mov      rbx, qword ptr [rsp + 0x48]
0000000140307a6d  call     qword ptr [rip + 0x2eeb85] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
0000000140307a73  mov      rcx, rax
0000000140307a76  xor      edx, edx
0000000140307a78  mov      r8, rbx
0000000140307a7b  call     qword ptr [rip + 0x2eeb6f] ; [0x1405f65f0] kernel32.dll!HeapFree
0000000140307a81  jmp      0x1403076d2 ; 
0000000140307a86  cmp      rbx, 4
0000000140307a8a  jne      0x140307a97
0000000140307a8c  cmp      dword ptr [rdi], 0x75706769
0000000140307a92  sete     cl
0000000140307a95  jmp      0x140307a99 ; 
0000000140307a97  xor      ecx, ecx
0000000140307a99  call     0x140308530 ; 
0000000140307a9e  mov      rcx, qword ptr [rsp + 0x58]
0000000140307aa3  mov      rdx, qword ptr [rsp + 0x60]
0000000140307aa8  call     0x14005afb0 ; 
0000000140307aad  mov      qword ptr [rsp + 0x1a0], rax
0000000140307ab5  mov      qword ptr [rsp + 0x1a8], rdx
0000000140307abd  mov      qword ptr [rsp + 0x90], r14
0000000140307ac5  lea      rax, [rip - 0x2ee69c] ; [0x140019430] 
0000000140307acc  mov      qword ptr [rsp + 0x98], rax
0000000140307ad4  lea      rax, [rsp + 0x1a0]
0000000140307adc  mov      qword ptr [rsp + 0xa0], rax
0000000140307ae4  lea      rax, [rip - 0x2eb14b] ; [0x14001c9a0] 
0000000140307aeb  mov      qword ptr [rsp + 0xa8], rax
0000000140307af3  lea      rcx, [rsp + 0x3f]
0000000140307af8  mov      qword ptr [rsp + 0xb0], rcx
0000000140307b00  lea      rcx, [rip - 0x2ed867] ; [0x14001a2a0] 
0000000140307b07  mov      qword ptr [rsp + 0xb8], rcx
0000000140307b0f  lea      rcx, [rsp + 0x190]
0000000140307b17  mov      qword ptr [rsp + 0xc0], rcx
0000000140307b1f  mov      qword ptr [rsp + 0xc8], rax
0000000140307b27  lea      rax, [rsp + 0xe0]
0000000140307b2f  mov      qword ptr [rsp + 0xd0], rax
0000000140307b37  lea      rax, [rip - 0x3029ee] ; [0x140005150] 
0000000140307b3e  mov      qword ptr [rsp + 0xd8], rax
0000000140307b46  lea      rax, [rip + 0x436a13] ; [0x14073e560] 'stdoutstderr'
0000000140307b4d  mov      qword ptr [rsp + 0x1b0], rax
0000000140307b55  mov      qword ptr [rsp + 0x1b8], 6
0000000140307b61  lea      rcx, [rip + 0x3b45b0] ; [0x1406bc118] rust-format-first-literal='[OpenRevo NVRAM] Successfully wrote '
0000000140307b68  lea      rdx, [rsp + 0x90]
0000000140307b70  call     0x140507680 ; 
0000000140307b75  test     al, al
0000000140307b77  jne      0x140307bbd
0000000140307b79  mov      eax, dword ptr [rip + 0x46ef31] ; [0x140776ab0] 
0000000140307b7f  test     eax, eax
0000000140307b81  jne      0x140307be6
0000000140307b83  lea      rax, [rip + 0x46eeee] ; [0x140776a78] rust-pieces=['UVW']
0000000140307b8a  mov      qword ptr [rsp + 0x100], rax
0000000140307b92  lea      rax, [rsp + 0x100]
0000000140307b9a  mov      qword ptr [rsp + 0x70], rax
0000000140307b9f  lea      rdx, [rip + 0x3b4572] ; [0x1406bc118] rust-format-first-literal='[OpenRevo NVRAM] Successfully wrote '
0000000140307ba6  lea      rcx, [rsp + 0x70]
0000000140307bab  lea      r8, [rsp + 0x90]
0000000140307bb3  call     0x140510e80 ; 
0000000140307bb8  test     rax, rax
0000000140307bbb  jne      0x140307bed
0000000140307bbd  mov      rcx, rsi
0000000140307bc0  call     0x140306f30 ; 
0000000140307bc5  cmp      qword ptr [rsp + 0x40], 0
0000000140307bcb  jne      0x140307a68
0000000140307bd1  jmp      0x1403076d2 ; 
0000000140307bd6  lea      r9, [rip + 0x3b45a3] ; [0x1406bc180] rust-pieces=['src\\driver\\uefi_nvram.rs']
0000000140307bdd  xor      ecx, ecx
0000000140307bdf  call     0x1405cc9e0 ; 
0000000140307be4  ud2      
0000000140307be6  call     0x1405ebb02 ; 
0000000140307beb  jmp      0x140307b83 ; 
0000000140307bed  mov      qword ptr [rsp + 0x108], rax
0000000140307bf5  lea      rax, [rsp + 0x1b0]
0000000140307bfd  mov      qword ptr [rsp + 0x70], rax
0000000140307c02  lea      rax, [rip + 0x207d37] ; [0x14050f940] 
0000000140307c09  mov      qword ptr [rsp + 0x78], rax
0000000140307c0e  lea      rax, [rsp + 0x108]
0000000140307c16  mov      qword ptr [rsp + 0x80], rax
0000000140307c1e  lea      rax, [rip - 0x2ef0e5] ; [0x140018b40] 
0000000140307c25  mov      qword ptr [rsp + 0x88], rax
0000000140307c2d  lea      rcx, [rip + 0x434f6c] ; [0x14073cba0] rust-format-first-literal='failed printing to '
0000000140307c34  lea      r8, [rip + 0x434fcd] ; [0x14073cc08] rust-pieces=['/rustc/88d9e12ae178fab0fb5cc050a94da85685d449ea/library\\std\\src\\io\\stdio.rs']
0000000140307c3b  lea      rdx, [rsp + 0x70]
0000000140307c40  call     0x1405cc9b0 ; 
0000000140307c45  ud2      
0000000140307c47  mov      ecx, 1
0000000140307c4c  mov      edx, 0x3c
0000000140307c51  call     0x1405cb7af ; 
0000000140307c56  ud2      
0000000140307c58  lea      rax, [rip + 0x3b44a1] ; [0x1406bc100] rust-pieces=['src\\driver\\uefi_nvram.rs']
0000000140307c5f  mov      ecx, 0x62
0000000140307c64  mov      rdx, r8
0000000140307c67  mov      r8, rax
0000000140307c6a  call     0x1405cc7d3 ; 
0000000140307c6f  ud2      
