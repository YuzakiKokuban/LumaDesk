; openrevo.exe SHA256=3e3b9d2297e36a0361d56c0f6357afdd9ebc8b201b943546c5cf8002cfabda09
; preferred image VA 0x14034f990..0x140350ae3
000000014034f990  push     r15
000000014034f992  push     r14
000000014034f994  push     r13
000000014034f996  push     r12
000000014034f998  push     rsi
000000014034f999  push     rdi
000000014034f99a  push     rbp
000000014034f99b  push     rbx
000000014034f99c  sub      rsp, 0x6d8
000000014034f9a3  mov      ebp, r9d
000000014034f9a6  mov      rbx, r8
000000014034f9a9  mov      rdi, rdx
000000014034f9ac  mov      r13, rcx
000000014034f9af  mov      eax, dword ptr [rip + 0x426feb] ; [0x1407769a0] 
000000014034f9b5  test     eax, eax
000000014034f9b7  jne      0x1403507c8
000000014034f9bd  cmp      byte ptr [rip + 0x426fe0], 0 ; [0x1407769a4] 
000000014034f9c4  je       0x1403507da
000000014034f9ca  lea      rcx, [rsp + 0x110]
000000014034f9d2  call     0x1403088f0 ; 
000000014034f9d7  mov      rdx, qword ptr [rbx + 8]
000000014034f9db  mov      r8, qword ptr [rbx + 0x10]
000000014034f9df  cmp      r8, 4
000000014034f9e3  jne      0x14034fa75
000000014034f9e9  movzx    eax, byte ptr [rdx]
000000014034f9ec  lea      ecx, [rax - 0x41]
000000014034f9ef  cmp      cl, 0x1a
000000014034f9f2  setb     cl
000000014034f9f5  shl      cl, 5
000000014034f9f8  or       cl, al
000000014034f9fa  cmp      cl, 0x69
000000014034f9fd  jne      0x14034fa75
000000014034f9ff  movzx    eax, byte ptr [rdx + 1]
000000014034fa03  lea      ecx, [rax - 0x41]
000000014034fa06  cmp      cl, 0x1a
000000014034fa09  setb     cl
000000014034fa0c  shl      cl, 5
000000014034fa0f  or       cl, al
000000014034fa11  cmp      cl, 0x67
000000014034fa14  jne      0x14034fa75
000000014034fa16  movzx    eax, byte ptr [rdx + 2]
000000014034fa1a  lea      ecx, [rax - 0x41]
000000014034fa1d  cmp      cl, 0x1a
000000014034fa20  setb     cl
000000014034fa23  shl      cl, 5
000000014034fa26  or       cl, al
000000014034fa28  cmp      cl, 0x70
000000014034fa2b  jne      0x14034fa75
000000014034fa2d  movzx    eax, byte ptr [rdx + 3]
000000014034fa31  lea      ecx, [rax - 0x41]
000000014034fa34  cmp      cl, 0x1a
000000014034fa37  setb     cl
000000014034fa3a  shl      cl, 5
000000014034fa3d  or       cl, al
000000014034fa3f  cmp      cl, 0x75
000000014034fa42  jne      0x14034fa75
000000014034fa44  cmp      byte ptr [rsp + 0x172], 0
000000014034fa4c  jne      0x14034fa75
000000014034fa4e  mov      rcx, qword ptr [rsp + 0x158]
000000014034fa56  cmp      rcx, -1
000000014034fa5a  je       0x1403506c4
000000014034fa60  mov      rax, qword ptr [rsp + 0x160]
000000014034fa68  mov      rsi, qword ptr [rsp + 0x168]
000000014034fa70  jmp      0x140350736 ; 
000000014034fa75  lea      rcx, [rsp + 0x3a0]
000000014034fa7d  call     0x140307500 ; 
000000014034fa82  mov      r15, qword ptr [rsp + 0x3a0]
000000014034fa8a  cmp      r15, -1
000000014034fa8e  je       0x14034fd90
000000014034fa94  lea      rsi, [rsp + 0x408]
000000014034fa9c  mov      rcx, rsi
000000014034fa9f  call     0x140336f40 ; 
000000014034faa4  cmp      qword ptr [rsp + 0x5b8], 0
000000014034faad  je       0x14034facb
000000014034faaf  mov      r14, qword ptr [rsp + 0x5c0]
000000014034fab7  call     qword ptr [rip + 0x2a6b3b] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014034fabd  mov      rcx, rax
000000014034fac0  xor      edx, edx
000000014034fac2  mov      r8, r14
000000014034fac5  call     qword ptr [rip + 0x2a6b25] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014034facb  mov      rax, qword ptr [rbx + 0x10]
000000014034facf  mov      qword ptr [rsi + 0x1c0], rax
000000014034fad6  movups   xmm0, xmmword ptr [rbx]
000000014034fad9  movups   xmmword ptr [rsi + 0x1b0], xmm0
000000014034fae0  lea      rcx, [rsp + 0x408]
000000014034fae8  call     0x140339f30 ; 
000000014034faed  test     bpl, 1
000000014034faf1  je       0x14034fbbc
000000014034faf7  lea      rax, [rip + 0x3eea62] ; [0x14073e560] 'stdoutstderr'
000000014034fafe  mov      qword ptr [rsp + 0x2b8], rax
000000014034fb06  mov      qword ptr [rsp + 0x2c0], 6
000000014034fb12  lea      rcx, [rip + 0x3aec10] ; [0x1406fe729] 
000000014034fb19  mov      edx, 0x9d
000000014034fb1e  call     0x140507680 ; 
000000014034fb23  test     al, al
000000014034fb25  jne      0x14034fb71
000000014034fb27  mov      eax, dword ptr [rip + 0x426f83] ; [0x140776ab0] 
000000014034fb2d  test     eax, eax
000000014034fb2f  jne      0x140350938
000000014034fb35  lea      rax, [rip + 0x426f3c] ; [0x140776a78] rust-pieces=['UVW']
000000014034fb3c  mov      qword ptr [rsp + 0x50], rax
000000014034fb41  lea      rax, [rsp + 0x50]
000000014034fb46  mov      qword ptr [rsp + 0x1d0], rax
000000014034fb4e  lea      rdx, [rip + 0x3aebd4] ; [0x1406fe729] 
000000014034fb55  lea      rcx, [rsp + 0x1d0]
000000014034fb5d  mov      r8d, 0x9d
000000014034fb63  call     0x140510e80 ; 
000000014034fb68  test     rax, rax
000000014034fb6b  jne      0x140350942
000000014034fb71  lea      rcx, [rsp + 0x1d0]
000000014034fb79  call     0x1403072a0 ; 
000000014034fb7e  cmp      qword ptr [rsp + 0x1d0], -1
000000014034fb87  je       0x14034fbbc
000000014034fb89  mov      rax, qword ptr [rsp + 0x1e0]
000000014034fb91  mov      qword ptr [r13 + 0x18], rax
000000014034fb95  movups   xmm0, xmmword ptr [rsp + 0x1d0]
000000014034fb9d  movups   xmmword ptr [r13 + 8], xmm0
000000014034fba2  mov      qword ptr [r13], 0xffffffffffffffff
000000014034fbaa  lea      rcx, [rsp + 0x408]
000000014034fbb2  call     0x1400a9170 ; 
000000014034fbb7  jmp      0x14034fe60 ; 
000000014034fbbc  mov      rdx, qword ptr [rsp + 0x740]
000000014034fbc4  lea      rcx, [rsp + 0x2b8]
000000014034fbcc  call     0x1403511e0 ; 
000000014034fbd1  mov      rbp, qword ptr [rsp + 0x2c8]
000000014034fbd9  mov      r15d, 1
000000014034fbdf  mov      ebx, 1
000000014034fbe4  test     rbp, rbp
000000014034fbe7  je       0x14034fc15
000000014034fbe9  mov      rsi, qword ptr [rsp + 0x2c0]
000000014034fbf1  xor      edx, edx
000000014034fbf3  mov      r8, rbp
000000014034fbf6  call     0x14050b9d0 ; 
000000014034fbfb  test     rax, rax
000000014034fbfe  je       0x140350a96
000000014034fc04  mov      rbx, rax
000000014034fc07  mov      rcx, rax
000000014034fc0a  mov      rdx, rsi
000000014034fc0d  mov      r8, rbp
000000014034fc10  call     0x1405caac0 ; 
000000014034fc15  movzx    r12d, byte ptr [rsp + 0x398]
000000014034fc1e  movzx    eax, byte ptr [rsp + 0x399]
000000014034fc26  mov      byte ptr [rsp + 0x37], al
000000014034fc2a  mov      r14, qword ptr [rsp + 0x2e0]
000000014034fc32  test     r14, r14
000000014034fc35  je       0x14034fc63
000000014034fc37  mov      rsi, qword ptr [rsp + 0x2d8]
000000014034fc3f  xor      edx, edx
000000014034fc41  mov      r8, r14
000000014034fc44  call     0x14050b9d0 ; 
000000014034fc49  test     rax, rax
000000014034fc4c  je       0x140350aa5
000000014034fc52  mov      r15, rax
000000014034fc55  mov      rcx, rax
000000014034fc58  mov      rdx, rsi
000000014034fc5b  mov      r8, r14
000000014034fc5e  call     0x1405caac0 ; 
000000014034fc63  mov      byte ptr [rsp + 0x36], r12b
000000014034fc68  mov      qword ptr [rsp + 0x1c8], r15
000000014034fc70  movzx    eax, byte ptr [rsp + 0x39a]
000000014034fc78  mov      byte ptr [rsp + 0x35], al
000000014034fc7c  mov      r8, qword ptr [rsp + 0x2f8]
000000014034fc84  mov      r12d, 1
000000014034fc8a  mov      eax, 1
000000014034fc8f  test     r8, r8
000000014034fc92  je       0x14034fccc
000000014034fc94  mov      rsi, qword ptr [rsp + 0x2f0]
000000014034fc9c  xor      edx, edx
000000014034fc9e  mov      r12, r8
000000014034fca1  call     0x14050b9d0 ; 
000000014034fca6  test     rax, rax
000000014034fca9  je       0x140350ab4
000000014034fcaf  mov      rcx, rax
000000014034fcb2  mov      rdx, rsi
000000014034fcb5  mov      r8, r12
000000014034fcb8  mov      rsi, rax
000000014034fcbb  call     0x1405caac0 ; 
000000014034fcc0  mov      rax, rsi
000000014034fcc3  mov      r8, r12
000000014034fcc6  mov      r12d, 1
000000014034fccc  mov      qword ptr [rsp + 0x1b8], rax
000000014034fcd4  mov      qword ptr [rsp + 0x1c0], r8
000000014034fcdc  mov      r8, qword ptr [rsp + 0x310]
000000014034fce4  test     r8, r8
000000014034fce7  je       0x14034fd18
000000014034fce9  mov      rsi, qword ptr [rsp + 0x308]
000000014034fcf1  xor      edx, edx
000000014034fcf3  mov      r15, r8
000000014034fcf6  call     0x14050b9d0 ; 
000000014034fcfb  test     rax, rax
000000014034fcfe  je       0x140350a67
000000014034fd04  mov      r12, rax
000000014034fd07  mov      rcx, rax
000000014034fd0a  mov      rdx, rsi
000000014034fd0d  mov      r8, r15
000000014034fd10  call     0x1405caac0 ; 
000000014034fd15  mov      r8, r15
000000014034fd18  mov      rsi, rbx
000000014034fd1b  movzx    eax, byte ptr [rsp + 0x39b]
000000014034fd23  mov      byte ptr [rsp + 0x34], al
000000014034fd27  mov      r15, 0xffffffffffffffff
000000014034fd2e  cmp      qword ptr [rsp + 0x380], -1
000000014034fd37  mov      qword ptr [rsp + 0x1b0], r8
000000014034fd3f  je       0x14034ff20
000000014034fd45  mov      rbx, qword ptr [rsp + 0x390]
000000014034fd4d  test     rbx, rbx
000000014034fd50  je       0x14034ff29
000000014034fd56  mov      r15, qword ptr [rsp + 0x388]
000000014034fd5e  xor      edx, edx
000000014034fd60  mov      r8, rbx
000000014034fd63  call     0x14050b9d0 ; 
000000014034fd68  test     rax, rax
000000014034fd6b  je       0x140350a76
000000014034fd71  mov      qword ptr [rsp + 0x40], rax
000000014034fd76  mov      rcx, rax
000000014034fd79  mov      rdx, r15
000000014034fd7c  mov      r8, rbx
000000014034fd7f  call     0x1405caac0 ; 
000000014034fd84  mov      r15, 0xffffffffffffffff
000000014034fd8b  jmp      0x14034ff35 ; 
000000014034fd90  lea      rax, [rsp + 0x3a8]
000000014034fd98  mov      qword ptr [rsp + 0xb0], rax
000000014034fda0  lea      rsi, [rsp + 0xb0]
000000014034fda8  mov      qword ptr [rsp + 0x2b8], rsi
000000014034fdb0  lea      r14, [rip - 0x3493d7] ; [0x1400069e0] 
000000014034fdb7  mov      qword ptr [rsp + 0x2c0], r14
000000014034fdbf  lea      rax, [rip + 0x3ee7a0] ; [0x14073e566] 'stderr'
000000014034fdc6  mov      qword ptr [rsp + 0x1d0], rax
000000014034fdce  mov      qword ptr [rsp + 0x1d8], 6
000000014034fdda  lea      rcx, [rip + 0x3ae8b0] ; [0x1406fe691] rust-format-first-literal='[OpenRevo GPU Mode] Hardware write failed: '
000000014034fde1  lea      rdx, [rsp + 0x2b8]
000000014034fde9  call     0x140507680 ; 
000000014034fdee  test     al, al
000000014034fdf0  jne      0x14034fe30
000000014034fdf2  lea      rax, [rip + 0x42997f] ; [0x140779778] 
000000014034fdf9  mov      qword ptr [rsp + 0x50], rax
000000014034fdfe  lea      rax, [rsp + 0x50]
000000014034fe03  mov      qword ptr [rsp + 0x408], rax
000000014034fe0b  lea      rdx, [rip + 0x3ae87f] ; [0x1406fe691] rust-format-first-literal='[OpenRevo GPU Mode] Hardware write failed: '
000000014034fe12  lea      rcx, [rsp + 0x408]
000000014034fe1a  lea      r8, [rsp + 0x2b8]
000000014034fe22  call     0x140511de0 ; 
000000014034fe27  test     rax, rax
000000014034fe2a  jne      0x1403508d5
000000014034fe30  mov      qword ptr [rsp + 0x408], rsi
000000014034fe38  mov      qword ptr [rsp + 0x410], r14
000000014034fe40  lea      rcx, [r13 + 8]
000000014034fe44  lea      rdx, [rip + 0x3ae876] ; [0x1406fe6c1] 
000000014034fe4b  lea      r8, [rsp + 0x408]
000000014034fe53  call     0x140003d70 ; 
000000014034fe58  mov      qword ptr [r13], 0xffffffffffffffff
000000014034fe60  lea      rcx, [rsp + 0x3a0]
000000014034fe68  call     0x140089220 ; 
000000014034fe6d  mov      rax, qword ptr [rsp + 0x158]
000000014034fe75  cmp      rax, -1
000000014034fe79  je       0x14034fe9c
000000014034fe7b  test     rax, rax
000000014034fe7e  je       0x14034fe9c
000000014034fe80  mov      rsi, qword ptr [rsp + 0x160]
000000014034fe88  call     qword ptr [rip + 0x2a676a] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014034fe8e  mov      rcx, rax
000000014034fe91  xor      edx, edx
000000014034fe93  mov      r8, rsi
000000014034fe96  call     qword ptr [rip + 0x2a6754] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014034fe9c  cmp      qword ptr [rsp + 0x110], 0
000000014034fea5  je       0x14034fec3
000000014034fea7  mov      rsi, qword ptr [rsp + 0x118]
000000014034feaf  call     qword ptr [rip + 0x2a6743] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014034feb5  mov      rcx, rax
000000014034feb8  xor      edx, edx
000000014034feba  mov      r8, rsi
000000014034febd  call     qword ptr [rip + 0x2a672d] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014034fec3  cmp      qword ptr [rsp + 0x128], 0
000000014034fecc  je       0x14034feea
000000014034fece  mov      rsi, qword ptr [rsp + 0x130]
000000014034fed6  call     qword ptr [rip + 0x2a671c] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014034fedc  mov      rcx, rax
000000014034fedf  xor      edx, edx
000000014034fee1  mov      r8, rsi
000000014034fee4  call     qword ptr [rip + 0x2a6706] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014034feea  cmp      qword ptr [rsp + 0x140], 0
000000014034fef3  je       0x14034ff11
000000014034fef5  mov      rsi, qword ptr [rsp + 0x148]
000000014034fefd  call     qword ptr [rip + 0x2a66f5] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014034ff03  mov      rcx, rax
000000014034ff06  xor      edx, edx
000000014034ff08  mov      r8, rsi
000000014034ff0b  call     qword ptr [rip + 0x2a66df] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014034ff11  cmp      r15, -1
000000014034ff15  jne      0x140350881
000000014034ff1b  jmp      0x140350863 ; 
000000014034ff20  mov      rbx, 0xffffffffffffffff
000000014034ff27  jmp      0x14034ff35 ; 
000000014034ff29  mov      eax, 1
000000014034ff2e  mov      qword ptr [rsp + 0x40], rax
000000014034ff33  xor      ebx, ebx
000000014034ff35  movzx    eax, byte ptr [rsp + 0x378]
000000014034ff3d  mov      byte ptr [rsp + 0x33], al
000000014034ff41  movzx    eax, byte ptr [rsp + 0x379]
000000014034ff49  mov      byte ptr [rsp + 0x32], al
000000014034ff4d  movzx    eax, byte ptr [rsp + 0x37a]
000000014034ff55  mov      byte ptr [rsp + 0x31], al
000000014034ff59  cmp      qword ptr [rsp + 0x360], -1
000000014034ff62  mov      qword ptr [rsp + 0x1a8], rsi
000000014034ff6a  mov      qword ptr [rsp + 0x1a0], r14
000000014034ff72  mov      qword ptr [rsp + 0x198], r12
000000014034ff7a  mov      qword ptr [rsp + 0x190], rbx
000000014034ff82  je       0x14034ffc1
000000014034ff84  mov      r15, qword ptr [rsp + 0x370]
000000014034ff8c  test     r15, r15
000000014034ff8f  je       0x14034ffc3
000000014034ff91  mov      rbx, qword ptr [rsp + 0x368]
000000014034ff99  xor      edx, edx
000000014034ff9b  mov      r8, r15
000000014034ff9e  call     0x14050b9d0 ; 
000000014034ffa3  test     rax, rax
000000014034ffa6  je       0x140350a67
000000014034ffac  mov      qword ptr [rsp + 0x38], rax
000000014034ffb1  mov      rcx, rax
000000014034ffb4  mov      rdx, rbx
000000014034ffb7  mov      r8, r15
000000014034ffba  call     0x1405caac0 ; 
000000014034ffbf  jmp      0x14034ffd0 ; 
000000014034ffc1  jmp      0x14034ffd0 ; 
000000014034ffc3  mov      eax, 1
000000014034ffc8  mov      qword ptr [rsp + 0x38], rax
000000014034ffcd  xor      r15d, r15d
000000014034ffd0  mov      rbx, qword ptr [rsp + 0x328]
000000014034ffd8  mov      r12d, 1
000000014034ffde  mov      esi, 1
000000014034ffe3  test     rbx, rbx
000000014034ffe6  je       0x140350014
000000014034ffe8  mov      r14, qword ptr [rsp + 0x320]
000000014034fff0  xor      edx, edx
000000014034fff2  mov      r8, rbx
000000014034fff5  call     0x14050b9d0 ; 
000000014034fffa  test     rax, rax
000000014034fffd  je       0x140350a76
0000000140350003  mov      rsi, rax
0000000140350006  mov      rcx, rax
0000000140350009  mov      rdx, r14
000000014035000c  mov      r8, rbx
000000014035000f  call     0x1405caac0 ; 
0000000140350014  mov      qword ptr [rsp + 0x188], rdi
000000014035001c  mov      qword ptr [rsp + 0x48], r13
0000000140350021  movzx    edi, byte ptr [rsp + 0x37b]
0000000140350029  movzx    eax, byte ptr [rsp + 0x37c]
0000000140350031  mov      byte ptr [rsp + 0x30], al
0000000140350035  mov      r13, qword ptr [rsp + 0x340]
000000014035003d  test     r13, r13
0000000140350040  je       0x14035006e
0000000140350042  mov      r14, qword ptr [rsp + 0x338]
000000014035004a  xor      edx, edx
000000014035004c  mov      r8, r13
000000014035004f  call     0x14050b9d0 ; 
0000000140350054  test     rax, rax
0000000140350057  je       0x140350ac3
000000014035005d  mov      r12, rax
0000000140350060  mov      rcx, rax
0000000140350063  mov      rdx, r14
0000000140350066  mov      r8, r13
0000000140350069  call     0x1405caac0 ; 
000000014035006e  mov      byte ptr [rsp + 0x2f], dil
0000000140350073  mov      qword ptr [rsp + 0x180], r15
000000014035007b  mov      rdi, qword ptr [rsp + 0x1c8]
0000000140350083  mov      r15, qword ptr [rsp + 0x358]
000000014035008b  test     r15, r15
000000014035008e  je       0x1403500ce
0000000140350090  mov      qword ptr [rsp + 0x178], rbp
0000000140350098  mov      rbp, qword ptr [rsp + 0x350]
00000001403500a0  xor      edx, edx
00000001403500a2  mov      r8, r15
00000001403500a5  call     0x14050b9d0 ; 
00000001403500aa  test     rax, rax
00000001403500ad  je       0x140350a67
00000001403500b3  mov      r14, rax
00000001403500b6  mov      rcx, rax
00000001403500b9  mov      rdx, rbp
00000001403500bc  mov      r8, r15
00000001403500bf  call     0x1405caac0 ; 
00000001403500c4  mov      rbp, qword ptr [rsp + 0x178]
00000001403500cc  jmp      0x1403500d4 ; 
00000001403500ce  mov      r14d, 1
00000001403500d4  mov      qword ptr [rsp + 0x1d0], rbp
00000001403500dc  mov      rax, qword ptr [rsp + 0x1a8]
00000001403500e4  mov      qword ptr [rsp + 0x1d8], rax
00000001403500ec  mov      qword ptr [rsp + 0x1e0], rbp
00000001403500f4  movzx    eax, byte ptr [rsp + 0x36]
00000001403500f9  mov      byte ptr [rsp + 0x2b0], al
0000000140350100  movzx    eax, byte ptr [rsp + 0x37]
0000000140350105  mov      byte ptr [rsp + 0x2b1], al
000000014035010c  mov      rax, qword ptr [rsp + 0x1a0]
0000000140350114  mov      qword ptr [rsp + 0x1e8], rax
000000014035011c  mov      qword ptr [rsp + 0x1f0], rdi
0000000140350124  mov      qword ptr [rsp + 0x1f8], rax
000000014035012c  movzx    eax, byte ptr [rsp + 0x35]
0000000140350131  mov      byte ptr [rsp + 0x2b2], al
0000000140350138  mov      rax, qword ptr [rsp + 0x1c0]
0000000140350140  mov      qword ptr [rsp + 0x200], rax
0000000140350148  mov      rcx, qword ptr [rsp + 0x1b8]
0000000140350150  mov      qword ptr [rsp + 0x208], rcx
0000000140350158  mov      qword ptr [rsp + 0x210], rax
0000000140350160  mov      rcx, qword ptr [rsp + 0x1b0]
0000000140350168  mov      qword ptr [rsp + 0x218], rcx
0000000140350170  mov      rax, qword ptr [rsp + 0x198]
0000000140350178  mov      qword ptr [rsp + 0x220], rax
0000000140350180  mov      qword ptr [rsp + 0x228], rcx
0000000140350188  movzx    eax, byte ptr [rsp + 0x34]
000000014035018d  mov      byte ptr [rsp + 0x2b3], al
0000000140350194  mov      rax, qword ptr [rsp + 0x190]
000000014035019c  mov      qword ptr [rsp + 0x298], rax
00000001403501a4  mov      rcx, qword ptr [rsp + 0x40]
00000001403501a9  mov      qword ptr [rsp + 0x2a0], rcx
00000001403501b1  mov      qword ptr [rsp + 0x2a8], rax
00000001403501b9  mov      qword ptr [rsp + 0x230], rbx
00000001403501c1  mov      qword ptr [rsp + 0x238], rsi
00000001403501c9  mov      qword ptr [rsp + 0x240], rbx
00000001403501d1  mov      qword ptr [rsp + 0x248], r13
00000001403501d9  mov      qword ptr [rsp + 0x250], r12
00000001403501e1  mov      qword ptr [rsp + 0x258], r13
00000001403501e9  mov      qword ptr [rsp + 0x260], r15
00000001403501f1  mov      qword ptr [rsp + 0x268], r14
00000001403501f9  mov      qword ptr [rsp + 0x270], r15
0000000140350201  mov      rax, qword ptr [rsp + 0x180]
0000000140350209  mov      qword ptr [rsp + 0x278], rax
0000000140350211  mov      rcx, qword ptr [rsp + 0x38]
0000000140350216  mov      qword ptr [rsp + 0x280], rcx
000000014035021e  mov      qword ptr [rsp + 0x288], rax
0000000140350226  movzx    eax, byte ptr [rsp + 0x33]
000000014035022b  mov      byte ptr [rsp + 0x290], al
0000000140350232  movzx    eax, byte ptr [rsp + 0x32]
0000000140350237  mov      byte ptr [rsp + 0x291], al
000000014035023e  movzx    eax, byte ptr [rsp + 0x31]
0000000140350243  mov      byte ptr [rsp + 0x292], al
000000014035024a  movzx    eax, byte ptr [rsp + 0x2f]
000000014035024f  mov      byte ptr [rsp + 0x293], al
0000000140350256  movzx    eax, byte ptr [rsp + 0x30]
000000014035025b  mov      byte ptr [rsp + 0x294], al
0000000140350262  lea      rcx, [rip + 0x3ae50e] ; [0x1406fe777] 'gpu-mode-changedsrc\\commands\\tuning_cmd.rs'
0000000140350269  mov      edx, 0x10
000000014035026e  call     0x140564270 ; 
0000000140350273  test     al, al
0000000140350275  mov      rdi, qword ptr [rsp + 0x188]
000000014035027d  je       0x140350480
0000000140350283  mov      r14, qword ptr [rdi + 0x88]
000000014035028a  mov      r8d, 0x10
0000000140350290  xor      edx, edx
0000000140350292  call     0x14050b9d0 ; 
0000000140350297  test     rax, rax
000000014035029a  mov      r13, qword ptr [rsp + 0x48]
000000014035029f  je       0x140350a85
00000001403502a5  mov      rsi, rax
00000001403502a8  movups   xmm0, xmmword ptr [rip + 0x3ae4c8] ; [0x1406fe777] 'gpu-mode-changedsrc\\commands\\tuning_cmd.rs'
00000001403502af  movups   xmmword ptr [rax], xmm0
00000001403502b2  lea      rcx, [rsp + 0x80]
00000001403502ba  lea      rdx, [rsp + 0x1d0]
00000001403502c2  call     0x1400afeb0 ; 
00000001403502c7  mov      rax, qword ptr [rsp + 0x80]
00000001403502cf  mov      rbx, qword ptr [rsp + 0x88]
00000001403502d7  cmp      rax, -1
00000001403502db  je       0x1403504d9
00000001403502e1  mov      rcx, qword ptr [rsp + 0x90]
00000001403502e9  mov      qword ptr [rsp + 0xb0], 0x10
00000001403502f5  mov      qword ptr [rsp + 0xb8], rsi
00000001403502fd  mov      qword ptr [rsp + 0xc0], 0x10
0000000140350309  mov      qword ptr [rsp + 0xc8], rax
0000000140350311  mov      qword ptr [rsp + 0xd0], rbx
0000000140350319  mov      qword ptr [rsp + 0xd8], rcx
0000000140350321  lea      rbx, [r14 + 0x1178]
0000000140350328  mov      cl, 1
000000014035032a  xor      eax, eax
000000014035032c  lock cmpxchg byte ptr [r14 + 0x1178], cl
0000000140350335  jne      0x1403509a5
000000014035033b  mov      rax, qword ptr [rip + 0x42940e] ; [0x140779750] 
0000000140350342  shl      rax, 1
0000000140350345  test     rax, rax
0000000140350348  jne      0x1403509b2
000000014035034e  xor      ebp, ebp
0000000140350350  movzx    eax, byte ptr [r14 + 0x1179]
0000000140350358  test     al, al
000000014035035a  jne      0x1403509c2
0000000140350360  lea      rsi, [r14 + 0x1400]
0000000140350367  mov      rax, qword ptr [r14 + 0x1180]
000000014035036e  mov      rcx, qword ptr [r14 + 0x1188]
0000000140350375  add      rcx, rax
0000000140350378  inc      rcx
000000014035037b  movdqa   xmm0, xmmword ptr [rax]
000000014035037f  pmovmskb edx, xmm0
0000000140350383  not      edx
0000000140350385  mov      r8, qword ptr [r14 + 0x1198]
000000014035038c  mov      qword ptr [rsp + 0x50], rax
0000000140350391  add      rax, 0x10
0000000140350395  mov      qword ptr [rsp + 0x58], rax
000000014035039a  mov      qword ptr [rsp + 0x60], rcx
000000014035039f  mov      word ptr [rsp + 0x68], dx
00000001403503a4  mov      qword ptr [rsp + 0x70], r8
00000001403503a9  lea      rcx, [rsp + 0x80]
00000001403503b1  lea      r8, [rsp + 0x50]
00000001403503b6  lea      r9, [rsp + 0xb0]
00000001403503be  mov      rdx, rsi
00000001403503c1  call     0x140070470 ; 
00000001403503c6  cmp      qword ptr [rsp + 0x80], -1
00000001403503cf  je       0x1403505f6
00000001403503d5  movups   xmm0, xmmword ptr [rsp + 0x80]
00000001403503dd  movups   xmm1, xmmword ptr [rsp + 0x90]
00000001403503e5  movups   xmm2, xmmword ptr [rsp + 0xa0]
00000001403503ed  movaps   xmmword ptr [rsp + 0x100], xmm2
00000001403503f5  movaps   xmmword ptr [rsp + 0xf0], xmm1
00000001403503fd  movaps   xmmword ptr [rsp + 0xe0], xmm0
0000000140350405  test     bpl, bpl
0000000140350408  jne      0x14035041d
000000014035040a  mov      rax, qword ptr [rip + 0x42933f] ; [0x140779750] 
0000000140350411  shl      rax, 1
0000000140350414  test     rax, rax
0000000140350417  jne      0x140350a22
000000014035041d  xor      eax, eax
000000014035041f  xchg     byte ptr [rbx], al
0000000140350421  cmp      al, 2
0000000140350423  je       0x1403509f7
0000000140350429  cmp      qword ptr [rsp + 0xb0], 0
0000000140350432  je       0x140350450
0000000140350434  mov      rsi, qword ptr [rsp + 0xb8]
000000014035043c  call     qword ptr [rip + 0x2a61b6] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
0000000140350442  mov      rcx, rax
0000000140350445  xor      edx, edx
0000000140350447  mov      r8, rsi
000000014035044a  call     qword ptr [rip + 0x2a61a0] ; [0x1405f65f0] kernel32.dll!HeapFree
0000000140350450  cmp      qword ptr [rsp + 0xc8], 0
0000000140350459  je       0x14035068e
000000014035045f  mov      rsi, qword ptr [rsp + 0xd0]
0000000140350467  call     qword ptr [rip + 0x2a618b] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014035046d  mov      rcx, rax
0000000140350470  xor      edx, edx
0000000140350472  mov      r8, rsi
0000000140350475  call     qword ptr [rip + 0x2a6175] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014035047b  jmp      0x14035068e ; 
0000000140350480  mov      r8d, 0x10
0000000140350486  xor      edx, edx
0000000140350488  call     0x14050b9d0 ; 
000000014035048d  test     rax, rax
0000000140350490  mov      r13, qword ptr [rsp + 0x48]
0000000140350495  je       0x140350a85
000000014035049b  movups   xmm0, xmmword ptr [rip + 0x3ae2d5] ; [0x1406fe777] 'gpu-mode-changedsrc\\commands\\tuning_cmd.rs'
00000001403504a2  movups   xmmword ptr [rax], xmm0
00000001403504a5  movabs   rcx, 0x8000000000000023
00000001403504af  mov      qword ptr [rsp + 0xe0], rcx
00000001403504b7  mov      qword ptr [rsp + 0xe8], 0x10
00000001403504c3  mov      qword ptr [rsp + 0xf0], rax
00000001403504cb  mov      qword ptr [rsp + 0xf8], 0x10
00000001403504d7  jmp      0x140350513 ; 
00000001403504d9  call     qword ptr [rip + 0x2a6119] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
00000001403504df  mov      rcx, rax
00000001403504e2  xor      edx, edx
00000001403504e4  mov      r8, rsi
00000001403504e7  call     qword ptr [rip + 0x2a6103] ; [0x1405f65f0] kernel32.dll!HeapFree
00000001403504ed  movabs   rax, 0x8000000000000005
00000001403504f7  mov      qword ptr [rsp + 0xe0], rax
00000001403504ff  mov      qword ptr [rsp + 0xe8], rbx
0000000140350507  mov      qword ptr [rsp + 0xf8], 0xffffffffffffffff
0000000140350513  lea      rcx, [rsp + 0x1d0]
000000014035051b  call     0x1400a95b0 ; 
0000000140350520  lea      rcx, [rsp + 0xe0]
0000000140350528  call     0x1400a20f0 ; 
000000014035052d  lea      rdx, [rsp + 0x2b8]
0000000140350535  mov      r8d, 0xe8
000000014035053b  mov      rcx, r13
000000014035053e  call     0x1405caac0 ; 
0000000140350543  lea      rcx, [rsp + 0x408]
000000014035054b  call     0x1400a9170 ; 
0000000140350550  lea      rcx, [rsp + 0x3a0]
0000000140350558  call     0x140089220 ; 
000000014035055d  mov      rax, qword ptr [rsp + 0x158]
0000000140350565  cmp      rax, -1
0000000140350569  je       0x14035058c
000000014035056b  test     rax, rax
000000014035056e  je       0x14035058c
0000000140350570  mov      rsi, qword ptr [rsp + 0x160]
0000000140350578  call     qword ptr [rip + 0x2a607a] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014035057e  mov      rcx, rax
0000000140350581  xor      edx, edx
0000000140350583  mov      r8, rsi
0000000140350586  call     qword ptr [rip + 0x2a6064] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014035058c  cmp      qword ptr [rsp + 0x110], 0
0000000140350595  je       0x1403505b3
0000000140350597  mov      rsi, qword ptr [rsp + 0x118]
000000014035059f  call     qword ptr [rip + 0x2a6053] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
00000001403505a5  mov      rcx, rax
00000001403505a8  xor      edx, edx
00000001403505aa  mov      r8, rsi
00000001403505ad  call     qword ptr [rip + 0x2a603d] ; [0x1405f65f0] kernel32.dll!HeapFree
00000001403505b3  cmp      qword ptr [rsp + 0x128], 0
00000001403505bc  je       0x1403505da
00000001403505be  mov      rsi, qword ptr [rsp + 0x130]
00000001403505c6  call     qword ptr [rip + 0x2a602c] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
00000001403505cc  mov      rcx, rax
00000001403505cf  xor      edx, edx
00000001403505d1  mov      r8, rsi
00000001403505d4  call     qword ptr [rip + 0x2a6016] ; [0x1405f65f0] kernel32.dll!HeapFree
00000001403505da  cmp      qword ptr [rsp + 0x140], 0
00000001403505e3  je       0x140350881
00000001403505e9  mov      rsi, qword ptr [rsp + 0x148]
00000001403505f1  jmp      0x14035086d ; 
00000001403505f6  test     bpl, bpl
00000001403505f9  jne      0x14035060e
00000001403505fb  mov      rax, qword ptr [rip + 0x42914e] ; [0x140779750] 
0000000140350602  shl      rax, 1
0000000140350605  test     rax, rax
0000000140350608  jne      0x140350a3c
000000014035060e  xor      eax, eax
0000000140350610  xchg     byte ptr [rbx], al
0000000140350612  cmp      al, 2
0000000140350614  je       0x140350a14
000000014035061a  movups   xmm0, xmmword ptr [rsp + 0xb0]
0000000140350622  movups   xmm1, xmmword ptr [rsp + 0xc0]
000000014035062a  movups   xmm2, xmmword ptr [rsp + 0xd0]
0000000140350632  movaps   xmmword ptr [rsp + 0xa0], xmm2
000000014035063a  movaps   xmmword ptr [rsp + 0x90], xmm1
0000000140350642  movaps   xmmword ptr [rsp + 0x80], xmm0
000000014035064a  lea      rcx, [rsp + 0x50]
000000014035064f  lea      r8, [rsp + 0x80]
0000000140350657  mov      rdx, rsi
000000014035065a  call     0x14052b100 ; 
000000014035065f  cmp      qword ptr [rsp + 0x50], -1
0000000140350665  je       0x1403506b2
0000000140350667  movups   xmm0, xmmword ptr [rsp + 0x50]
000000014035066c  movups   xmm1, xmmword ptr [rsp + 0x60]
0000000140350671  movups   xmm2, xmmword ptr [rsp + 0x70]
0000000140350676  movaps   xmmword ptr [rsp + 0x100], xmm2
000000014035067e  movaps   xmmword ptr [rsp + 0xf0], xmm1
0000000140350686  movaps   xmmword ptr [rsp + 0xe0], xmm0
000000014035068e  mov      rsi, qword ptr [rsp + 0xe0]
0000000140350696  lea      rcx, [rsp + 0x1d0]
000000014035069e  call     0x1400a95b0 ; 
00000001403506a3  cmp      rsi, -1
00000001403506a7  jne      0x140350520
00000001403506ad  jmp      0x14035052d ; 
00000001403506b2  lea      rcx, [rsp + 0x1d0]
00000001403506ba  call     0x1400a95b0 ; 
00000001403506bf  jmp      0x14035052d ; 
00000001403506c4  mov      esi, 0x72
00000001403506c9  mov      r8d, 0x72
00000001403506cf  xor      edx, edx
00000001403506d1  call     0x14050b9d0 ; 
00000001403506d6  test     rax, rax
00000001403506d9  je       0x140350ad2
00000001403506df  movups   xmm0, xmmword ptr [rip + 0x30ef72] ; [0x14065f658] 
00000001403506e6  movups   xmmword ptr [rax + 0x60], xmm0
00000001403506ea  movups   xmm0, xmmword ptr [rip + 0x30ef57] ; [0x14065f648] 
00000001403506f1  movups   xmmword ptr [rax + 0x50], xmm0
00000001403506f5  movups   xmm0, xmmword ptr [rip + 0x30ef3c] ; [0x14065f638] 
00000001403506fc  movups   xmmword ptr [rax + 0x40], xmm0
0000000140350700  movups   xmm0, xmmword ptr [rip + 0x30ef21] ; [0x14065f628] 
0000000140350707  movups   xmmword ptr [rax + 0x30], xmm0
000000014035070b  movups   xmm0, xmmword ptr [rip + 0x30ef06] ; [0x14065f618] 
0000000140350712  movups   xmmword ptr [rax + 0x20], xmm0
0000000140350716  movups   xmm0, xmmword ptr [rip + 0x30eeeb] ; [0x14065f608] 
000000014035071d  movups   xmmword ptr [rax + 0x10], xmm0
0000000140350721  movups   xmm0, xmmword ptr [rip + 0x30eed0] ; [0x14065f5f8] 
0000000140350728  movups   xmmword ptr [rax], xmm0
000000014035072b  mov      word ptr [rax + 0x70], 0x81bc
0000000140350731  mov      ecx, 0x72
0000000140350736  mov      qword ptr [r13 + 8], rcx
000000014035073a  mov      qword ptr [r13 + 0x10], rax
000000014035073e  mov      qword ptr [r13 + 0x18], rsi
0000000140350742  mov      qword ptr [r13], 0xffffffffffffffff
000000014035074a  cmp      qword ptr [rsp + 0x110], 0
0000000140350753  je       0x140350771
0000000140350755  mov      rsi, qword ptr [rsp + 0x118]
000000014035075d  call     qword ptr [rip + 0x2a5e95] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
0000000140350763  mov      rcx, rax
0000000140350766  xor      edx, edx
0000000140350768  mov      r8, rsi
000000014035076b  call     qword ptr [rip + 0x2a5e7f] ; [0x1405f65f0] kernel32.dll!HeapFree
0000000140350771  cmp      qword ptr [rsp + 0x128], 0
000000014035077a  je       0x140350798
000000014035077c  mov      rsi, qword ptr [rsp + 0x130]
0000000140350784  call     qword ptr [rip + 0x2a5e6e] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014035078a  mov      rcx, rax
000000014035078d  xor      edx, edx
000000014035078f  mov      r8, rsi
0000000140350792  call     qword ptr [rip + 0x2a5e58] ; [0x1405f65f0] kernel32.dll!HeapFree
0000000140350798  cmp      qword ptr [rsp + 0x140], 0
00000001403507a1  je       0x140350863
00000001403507a7  mov      rsi, qword ptr [rsp + 0x148]
00000001403507af  call     qword ptr [rip + 0x2a5e43] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
00000001403507b5  mov      rcx, rax
00000001403507b8  xor      edx, edx
00000001403507ba  mov      r8, rsi
00000001403507bd  call     qword ptr [rip + 0x2a5e2d] ; [0x1405f65f0] kernel32.dll!HeapFree
00000001403507c3  jmp      0x140350863 ; 
00000001403507c8  call     0x1405cf089 ; 
00000001403507cd  cmp      byte ptr [rip + 0x4261d0], 0 ; [0x1407769a4] 
00000001403507d4  jne      0x14034f9ca
00000001403507da  mov      r8d, 0x80
00000001403507e0  xor      edx, edx
00000001403507e2  call     0x14050b9d0 ; 
00000001403507e7  test     rax, rax
00000001403507ea  je       0x140350a56
00000001403507f0  movups   xmm0, xmmword ptr [rip + 0x3ade8a] ; [0x1406fe681] 
00000001403507f7  movups   xmmword ptr [rax + 0x70], xmm0
00000001403507fb  movups   xmm0, xmmword ptr [rip + 0x3ade6f] ; [0x1406fe671] 
0000000140350802  movups   xmmword ptr [rax + 0x60], xmm0
0000000140350806  movups   xmm0, xmmword ptr [rip + 0x3ade54] ; [0x1406fe661] 
000000014035080d  movups   xmmword ptr [rax + 0x50], xmm0
0000000140350811  movups   xmm0, xmmword ptr [rip + 0x3ade39] ; [0x1406fe651] 
0000000140350818  movups   xmmword ptr [rax + 0x40], xmm0
000000014035081c  movups   xmm0, xmmword ptr [rip + 0x3ade1e] ; [0x1406fe641] 
0000000140350823  movups   xmmword ptr [rax + 0x30], xmm0
0000000140350827  movups   xmm0, xmmword ptr [rip + 0x3ade03] ; [0x1406fe631] 
000000014035082e  movups   xmmword ptr [rax + 0x20], xmm0
0000000140350832  movups   xmm0, xmmword ptr [rip + 0x3adde8] ; [0x1406fe621] 
0000000140350839  movups   xmmword ptr [rax + 0x10], xmm0
000000014035083d  movups   xmm0, xmmword ptr [rip + 0x3addcd] ; [0x1406fe611] 
0000000140350844  movups   xmmword ptr [rax], xmm0
0000000140350847  mov      qword ptr [r13 + 8], 0x80
000000014035084f  mov      qword ptr [r13 + 0x10], rax
0000000140350853  mov      qword ptr [r13 + 0x18], 0x80
000000014035085b  mov      qword ptr [r13], 0xffffffffffffffff
0000000140350863  cmp      qword ptr [rbx], 0
0000000140350867  je       0x140350881
0000000140350869  mov      rsi, qword ptr [rbx + 8]
000000014035086d  call     qword ptr [rip + 0x2a5d85] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
0000000140350873  mov      rcx, rax
0000000140350876  xor      edx, edx
0000000140350878  mov      r8, rsi
000000014035087b  call     qword ptr [rip + 0x2a5d6f] ; [0x1405f65f0] kernel32.dll!HeapFree
0000000140350881  mov      rcx, rdi
0000000140350884  call     0x140087170 ; 
0000000140350889  mov      rax, qword ptr [rdi + 0x88]
0000000140350890  lock dec qword ptr [rax]
0000000140350894  jne      0x1403508a2
0000000140350896  lea      rcx, [rdi + 0x88]
000000014035089d  call     0x14055df80 ; 
00000001403508a2  mov      rax, qword ptr [rdi + 0x90]
00000001403508a9  lock dec qword ptr [rax]
00000001403508ad  jne      0x1403508be
00000001403508af  add      rdi, 0x90
00000001403508b6  mov      rcx, rdi
00000001403508b9  call     0x1400446a0 ; 
00000001403508be  mov      rax, r13
00000001403508c1  add      rsp, 0x6d8
00000001403508c8  pop      rbx
00000001403508c9  pop      rbp
00000001403508ca  pop      rdi
00000001403508cb  pop      rsi
00000001403508cc  pop      r12
00000001403508ce  pop      r13
00000001403508d0  pop      r14
00000001403508d2  pop      r15
00000001403508d4  ret      
00000001403508d5  mov      qword ptr [rsp + 0x80], rax
00000001403508dd  lea      rax, [rsp + 0x1d0]
00000001403508e5  mov      qword ptr [rsp + 0x408], rax
00000001403508ed  lea      rax, [rip + 0x1bf04c] ; [0x14050f940] 
00000001403508f4  mov      qword ptr [rsp + 0x410], rax
00000001403508fc  lea      rax, [rsp + 0x80]
0000000140350904  mov      qword ptr [rsp + 0x418], rax
000000014035090c  lea      rax, [rip - 0x337dd3] ; [0x140018b40] 
0000000140350913  mov      qword ptr [rsp + 0x420], rax
000000014035091b  lea      rcx, [rip + 0x3ec27e] ; [0x14073cba0] rust-format-first-literal='failed printing to '
0000000140350922  lea      r8, [rip + 0x3ec2df] ; [0x14073cc08] rust-pieces=['/rustc/88d9e12ae178fab0fb5cc050a94da85685d449ea/library\\std\\src\\io\\stdio.rs']
0000000140350929  lea      rdx, [rsp + 0x408]
0000000140350931  call     0x1405cc9b0 ; 
0000000140350936  ud2      
0000000140350938  call     0x1405ebb02 ; 
000000014035093d  jmp      0x14034fb35 ; 
0000000140350942  mov      qword ptr [rsp + 0x80], rax
000000014035094a  lea      rax, [rsp + 0x2b8]
0000000140350952  mov      qword ptr [rsp + 0x1d0], rax
000000014035095a  lea      rax, [rip + 0x1befdf] ; [0x14050f940] 
0000000140350961  mov      qword ptr [rsp + 0x1d8], rax
0000000140350969  lea      rax, [rsp + 0x80]
0000000140350971  mov      qword ptr [rsp + 0x1e0], rax
0000000140350979  lea      rax, [rip - 0x337e40] ; [0x140018b40] 
0000000140350980  mov      qword ptr [rsp + 0x1e8], rax
0000000140350988  lea      rcx, [rip + 0x3ec211] ; [0x14073cba0] rust-format-first-literal='failed printing to '
000000014035098f  lea      r8, [rip + 0x3ec272] ; [0x14073cc08] rust-pieces=['/rustc/88d9e12ae178fab0fb5cc050a94da85685d449ea/library\\std\\src\\io\\stdio.rs']
0000000140350996  lea      rdx, [rsp + 0x1d0]
000000014035099e  call     0x1405cc9b0 ; 
00000001403509a3  ud2      
00000001403509a5  mov      rcx, rbx
00000001403509a8  call     0x1405ec170 ; 
00000001403509ad  jmp      0x14035033b ; 
00000001403509b2  call     0x1405ece80 ; 
00000001403509b7  mov      ebp, eax
00000001403509b9  xor      bpl, 1
00000001403509bd  jmp      0x140350350 ; 
00000001403509c2  mov      qword ptr [rsp + 0x50], rbx
00000001403509c7  mov      byte ptr [rsp + 0x58], bpl
00000001403509cc  lea      rax, [rip + 0x2f92ed] ; [0x140649cc0] rust-pieces=['C:\\Users\\faint\\.cargo\\registry\\src\\index.crates.io-1949cf8c6b5b557f\\tauri-2.11.5\\src\\manager\\webview.rs']
00000001403509d3  mov      qword ptr [rsp + 0x20], rax
00000001403509d8  lea      rcx, [rip + 0x2f9261] ; [0x140649c40] 'poisoned webview managerC:\\Users\\faint\\.cargo\\registry\\src\\index.crates.io-1949cf8c6b5b557f\\tauri-2.11.5\\src\\manager\\webview.rs'
00000001403509df  lea      r9, [rip + 0x361c32] ; [0x1406b2618] 
00000001403509e6  lea      r8, [rsp + 0x50]
00000001403509eb  mov      edx, 0x18
00000001403509f0  call     0x1405cc750 ; 
00000001403509f5  ud2      
00000001403509f7  mov      rcx, rbx
00000001403509fa  call     qword ptr [rip + 0x2a57b8] ; [0x1405f61b8] api-ms-win-core-synch-l1-2-0.dll!WakeByAddressSingle
0000000140350a00  cmp      qword ptr [rsp + 0xb0], 0
0000000140350a09  jne      0x140350434
0000000140350a0f  jmp      0x140350450 ; 
0000000140350a14  mov      rcx, rbx
0000000140350a17  call     qword ptr [rip + 0x2a579b] ; [0x1405f61b8] api-ms-win-core-synch-l1-2-0.dll!WakeByAddressSingle
0000000140350a1d  jmp      0x14035061a ; 
0000000140350a22  call     0x1405ece80 ; 
0000000140350a27  test     al, al
0000000140350a29  jne      0x14035041d
0000000140350a2f  mov      byte ptr [r14 + 0x1179], 1
0000000140350a37  jmp      0x14035041d ; 
0000000140350a3c  call     0x1405ece80 ; 
0000000140350a41  test     al, al
0000000140350a43  jne      0x14035060e
0000000140350a49  mov      byte ptr [r14 + 0x1179], 1
0000000140350a51  jmp      0x14035060e ; 
0000000140350a56  mov      ecx, 1
0000000140350a5b  mov      edx, 0x80
0000000140350a60  call     0x1405cb7af ; 
0000000140350a65  ud2      
0000000140350a67  mov      ecx, 1
0000000140350a6c  mov      rdx, r15
0000000140350a6f  call     0x1405cb7af ; 
0000000140350a74  ud2      
0000000140350a76  mov      ecx, 1
0000000140350a7b  mov      rdx, rbx
0000000140350a7e  call     0x1405cb7af ; 
0000000140350a83  ud2      
0000000140350a85  mov      ecx, 1
0000000140350a8a  mov      edx, 0x10
0000000140350a8f  call     0x1405cb7af ; 
0000000140350a94  ud2      
0000000140350a96  mov      ecx, 1
0000000140350a9b  mov      rdx, rbp
0000000140350a9e  call     0x1405cb7af ; 
0000000140350aa3  ud2      
0000000140350aa5  mov      ecx, 1
0000000140350aaa  mov      rdx, r14
0000000140350aad  call     0x1405cb7af ; 
0000000140350ab2  ud2      
0000000140350ab4  mov      ecx, 1
0000000140350ab9  mov      rdx, r12
0000000140350abc  call     0x1405cb7af ; 
0000000140350ac1  ud2      
0000000140350ac3  mov      ecx, 1
0000000140350ac8  mov      rdx, r13
0000000140350acb  call     0x1405cb7af ; 
0000000140350ad0  ud2      
0000000140350ad2  mov      ecx, 1
0000000140350ad7  mov      edx, 0x72
0000000140350adc  call     0x1405cb7af ; 
0000000140350ae1  ud2      
