; oem-driver.sys SHA256=6e360ee4a0c42b2a4eaf46208c1592c87f8cd71aca2a6c52831a2ad356e61235
; preferred image VA 0x140002870..0x1400030a4
0000000140002870  mov      qword ptr [rsp + 0x20], r9
0000000140002875  mov      qword ptr [rsp + 0x18], r8
000000014000287a  mov      qword ptr [rsp + 0x10], rdx
000000014000287f  mov      qword ptr [rsp + 8], rcx
0000000140002884  sub      rsp, 0x478
000000014000288b  mov      rax, qword ptr [rip + 0x37ae] ; [0x140006040] 
0000000140002892  xor      rax, rsp
0000000140002895  mov      qword ptr [rsp + 0x460], rax
000000014000289d  mov      dword ptr [rsp + 0x20], 0
00000001400028a5  mov      qword ptr [rsp + 0x30], 0
00000001400028ae  mov      qword ptr [rsp + 0x38], 0
00000001400028b7  lea      rax, [rip + 0x19b2] ; [0x140004270] '0000'
00000001400028be  mov      qword ptr [rsp + 0x28], rax
00000001400028c3  cmp      qword ptr [rsp + 0x490], 0
00000001400028cc  je       0x1400028d9
00000001400028ce  cmp      qword ptr [rsp + 0x498], 0
00000001400028d7  jne      0x1400028f1
00000001400028d9  mov      edx, 0xc000000d
00000001400028de  mov      rcx, qword ptr [rsp + 0x488]
00000001400028e6  call     0x140003370 ; 
00000001400028eb  nop      
00000001400028ec  jmp      0x14000308c ; 
00000001400028f1  mov      rcx, qword ptr [rsp + 0x480]
00000001400028f9  call     0x140003254 ; 
00000001400028fe  mov      qword ptr [rsp + 0x40], rax
0000000140002903  lea      r9, [rsp + 0x50]
0000000140002908  lea      r8, [rsp + 0x30]
000000014000290d  xor      edx, edx
000000014000290f  mov      rcx, qword ptr [rsp + 0x488]
0000000140002917  call     0x1400033bc ; 
000000014000291c  mov      dword ptr [rsp + 0x20], eax
0000000140002920  mov      rdx, qword ptr [rsp + 0x498]
0000000140002928  mov      rcx, qword ptr [rsp + 0x30]
000000014000292d  call     0x14000281c ; 
0000000140002932  lea      r9, [rsp + 0x50]
0000000140002937  lea      r8, [rsp + 0x38]
000000014000293c  xor      edx, edx
000000014000293e  mov      rcx, qword ptr [rsp + 0x488]
0000000140002946  call     0x140003420 ; 
000000014000294b  mov      dword ptr [rsp + 0x20], eax
000000014000294f  cmp      dword ptr [rsp + 0x20], 0
0000000140002954  jge      0x140002963
0000000140002956  mov      dword ptr [rsp + 0x20], 0xc000009a
000000014000295e  jmp      0x14000308c ; 
0000000140002963  mov      eax, dword ptr [rsp + 0x4a0]
000000014000296a  mov      dword ptr [rsp + 0x24], eax
000000014000296e  cmp      dword ptr [rsp + 0x24], 0x9c40a4c4
0000000140002976  ja       0x140002a38
000000014000297c  cmp      dword ptr [rsp + 0x24], 0x9c40a4c4
0000000140002984  je       0x140002e2b
000000014000298a  cmp      dword ptr [rsp + 0x24], 0x9c40a494
0000000140002992  ja       0x1400029ed
0000000140002994  cmp      dword ptr [rsp + 0x24], 0x9c40a494
000000014000299c  je       0x140002c9f
00000001400029a2  cmp      dword ptr [rsp + 0x24], 0x9c40a480
00000001400029aa  je       0x140002bd9
00000001400029b0  cmp      dword ptr [rsp + 0x24], 0x9c40a484
00000001400029b8  je       0x140002c1b
00000001400029be  cmp      dword ptr [rsp + 0x24], 0x9c40a488
00000001400029c6  je       0x140002ad4
00000001400029cc  cmp      dword ptr [rsp + 0x24], 0x9c40a48c
00000001400029d4  je       0x140002b05
00000001400029da  cmp      dword ptr [rsp + 0x24], 0x9c40a490
00000001400029e2  je       0x140002c5d
00000001400029e8  jmp      0x140003067 ; 
00000001400029ed  cmp      dword ptr [rsp + 0x24], 0x9c40a498
00000001400029f5  je       0x140002ce1
00000001400029fb  cmp      dword ptr [rsp + 0x24], 0x9c40a49c
0000000140002a03  je       0x140002d23
0000000140002a09  cmp      dword ptr [rsp + 0x24], 0x9c40a4a0
0000000140002a11  je       0x140002d65
0000000140002a17  cmp      dword ptr [rsp + 0x24], 0x9c40a4a4
0000000140002a1f  je       0x140002da7
0000000140002a25  cmp      dword ptr [rsp + 0x24], 0x9c40a4c0
0000000140002a2d  je       0x140002de9
0000000140002a33  jmp      0x140003067 ; 
0000000140002a38  cmp      dword ptr [rsp + 0x24], 0x9c40a4dc
0000000140002a40  ja       0x140002a9b
0000000140002a42  cmp      dword ptr [rsp + 0x24], 0x9c40a4dc
0000000140002a4a  je       0x140002fb7
0000000140002a50  cmp      dword ptr [rsp + 0x24], 0x9c40a4c8
0000000140002a58  je       0x140002e6d
0000000140002a5e  cmp      dword ptr [rsp + 0x24], 0x9c40a4cc
0000000140002a66  je       0x140002eaf
0000000140002a6c  cmp      dword ptr [rsp + 0x24], 0x9c40a4d0
0000000140002a74  je       0x140002ef1
0000000140002a7a  cmp      dword ptr [rsp + 0x24], 0x9c40a4d4
0000000140002a82  je       0x140002f33
0000000140002a88  cmp      dword ptr [rsp + 0x24], 0x9c40a4d8
0000000140002a90  je       0x140002f75
0000000140002a96  jmp      0x140003067 ; 
0000000140002a9b  cmp      dword ptr [rsp + 0x24], 0x9c40a4e0
0000000140002aa3  je       0x140002ff3
0000000140002aa9  cmp      dword ptr [rsp + 0x24], 0x9c40a4e4
0000000140002ab1  je       0x14000302f
0000000140002ab7  cmp      dword ptr [rsp + 0x24], 0x9c40a500
0000000140002abf  je       0x140002b36
0000000140002ac1  cmp      dword ptr [rsp + 0x24], 0x9c40a504
0000000140002ac9  je       0x140002b67
0000000140002acf  jmp      0x140003067 ; 
0000000140002ad4  mov      r8, qword ptr [rsp + 0x38]
0000000140002ad9  mov      rdx, qword ptr [rsp + 0x30]
0000000140002ade  mov      rcx, qword ptr [rsp + 0x40]
0000000140002ae3  call     0x140002130 ; 
0000000140002ae8  mov      dword ptr [rsp + 0x20], eax
0000000140002aec  cmp      dword ptr [rsp + 0x20], 0
0000000140002af1  jge      0x140002b00
0000000140002af3  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002afb  jmp      0x140003067 ; 
0000000140002b00  jmp      0x140003067 ; 
0000000140002b05  mov      r8, qword ptr [rsp + 0x38]
0000000140002b0a  mov      rdx, qword ptr [rsp + 0x30]
0000000140002b0f  mov      rcx, qword ptr [rsp + 0x40]
0000000140002b14  call     0x1400025b8 ; 
0000000140002b19  mov      dword ptr [rsp + 0x20], eax
0000000140002b1d  cmp      dword ptr [rsp + 0x20], 0
0000000140002b22  jge      0x140002b31
0000000140002b24  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002b2c  jmp      0x140003067 ; 
0000000140002b31  jmp      0x140003067 ; 
0000000140002b36  mov      r8, qword ptr [rsp + 0x38]
0000000140002b3b  mov      rdx, qword ptr [rsp + 0x30]
0000000140002b40  mov      rcx, qword ptr [rsp + 0x40]
0000000140002b45  call     0x140002370 ; 
0000000140002b4a  mov      dword ptr [rsp + 0x20], eax
0000000140002b4e  cmp      dword ptr [rsp + 0x20], 0
0000000140002b53  jge      0x140002b62
0000000140002b55  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002b5d  jmp      0x140003067 ; 
0000000140002b62  jmp      0x140003067 ; 
0000000140002b67  mov      r9d, 4
0000000140002b6d  xor      r8d, r8d
0000000140002b70  mov      rdx, qword ptr [rsp + 0x30]
0000000140002b75  lea      rcx, [rsp + 0x48]
0000000140002b7a  call     0x14000351c ; 
0000000140002b7f  mov      rax, qword ptr [rsp + 0x498]
0000000140002b87  sub      rax, 4
0000000140002b8b  mov      r9, rax
0000000140002b8e  mov      r8d, 4
0000000140002b94  mov      rdx, qword ptr [rsp + 0x30]
0000000140002b99  lea      rcx, [rsp + 0x60]
0000000140002b9e  call     0x14000351c ; 
0000000140002ba3  mov      r9, qword ptr [rsp + 0x38]
0000000140002ba8  lea      r8, [rsp + 0x60]
0000000140002bad  lea      rdx, [rsp + 0x48]
0000000140002bb2  mov      rcx, qword ptr [rsp + 0x40]
0000000140002bb7  call     0x140001784 ; 
0000000140002bbc  mov      dword ptr [rsp + 0x20], eax
0000000140002bc0  cmp      dword ptr [rsp + 0x20], 0
0000000140002bc5  jge      0x140002bd4
0000000140002bc7  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002bcf  jmp      0x140003067 ; 
0000000140002bd4  jmp      0x140003067 ; 
0000000140002bd9  lea      rax, [rip + 0x16a0] ; [0x140004280] 'SMCR'
0000000140002be0  mov      qword ptr [rsp + 0x28], rax
0000000140002be5  mov      r9, qword ptr [rsp + 0x38]
0000000140002bea  mov      r8, qword ptr [rsp + 0x30]
0000000140002bef  mov      rdx, qword ptr [rsp + 0x28]
0000000140002bf4  mov      rcx, qword ptr [rsp + 0x40]
0000000140002bf9  call     0x140001a98 ; 
0000000140002bfe  mov      dword ptr [rsp + 0x20], eax
0000000140002c02  cmp      dword ptr [rsp + 0x20], 0
0000000140002c07  jge      0x140002c16
0000000140002c09  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002c11  jmp      0x140003067 ; 
0000000140002c16  jmp      0x140003067 ; 
0000000140002c1b  lea      rax, [rip + 0x166e] ; [0x140004290] 'SMCW'
0000000140002c22  mov      qword ptr [rsp + 0x28], rax
0000000140002c27  mov      r9, qword ptr [rsp + 0x38]
0000000140002c2c  mov      r8, qword ptr [rsp + 0x30]
0000000140002c31  mov      rdx, qword ptr [rsp + 0x28]
0000000140002c36  mov      rcx, qword ptr [rsp + 0x40]
0000000140002c3b  call     0x140001dc0 ; 
0000000140002c40  mov      dword ptr [rsp + 0x20], eax
0000000140002c44  cmp      dword ptr [rsp + 0x20], 0
0000000140002c49  jge      0x140002c58
0000000140002c4b  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002c53  jmp      0x140003067 ; 
0000000140002c58  jmp      0x140003067 ; 
0000000140002c5d  lea      rax, [rip + 0x163c] ; [0x1400042a0] 'BRMM'
0000000140002c64  mov      qword ptr [rsp + 0x28], rax
0000000140002c69  mov      r9, qword ptr [rsp + 0x38]
0000000140002c6e  mov      r8, qword ptr [rsp + 0x30]
0000000140002c73  mov      rdx, qword ptr [rsp + 0x28]
0000000140002c78  mov      rcx, qword ptr [rsp + 0x40]
0000000140002c7d  call     0x140001784 ; 
0000000140002c82  mov      dword ptr [rsp + 0x20], eax
0000000140002c86  cmp      dword ptr [rsp + 0x20], 0
0000000140002c8b  jge      0x140002c9a
0000000140002c8d  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002c95  jmp      0x140003067 ; 
0000000140002c9a  jmp      0x140003067 ; 
0000000140002c9f  lea      rax, [rip + 0x160a] ; [0x1400042b0] 'DRMM'
0000000140002ca6  mov      qword ptr [rsp + 0x28], rax
0000000140002cab  mov      r9, qword ptr [rsp + 0x38]
0000000140002cb0  mov      r8, qword ptr [rsp + 0x30]
0000000140002cb5  mov      rdx, qword ptr [rsp + 0x28]
0000000140002cba  mov      rcx, qword ptr [rsp + 0x40]
0000000140002cbf  call     0x140001784 ; 
0000000140002cc4  mov      dword ptr [rsp + 0x20], eax
0000000140002cc8  cmp      dword ptr [rsp + 0x20], 0
0000000140002ccd  jge      0x140002cdc
0000000140002ccf  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002cd7  jmp      0x140003067 ; 
0000000140002cdc  jmp      0x140003067 ; 
0000000140002ce1  lea      rax, [rip + 0x15d8] ; [0x1400042c0] 'BWMM'
0000000140002ce8  mov      qword ptr [rsp + 0x28], rax
0000000140002ced  mov      r9, qword ptr [rsp + 0x38]
0000000140002cf2  mov      r8, qword ptr [rsp + 0x30]
0000000140002cf7  mov      rdx, qword ptr [rsp + 0x28]
0000000140002cfc  mov      rcx, qword ptr [rsp + 0x40]
0000000140002d01  call     0x140001a98 ; 
0000000140002d06  mov      dword ptr [rsp + 0x20], eax
0000000140002d0a  cmp      dword ptr [rsp + 0x20], 0
0000000140002d0f  jge      0x140002d1e
0000000140002d11  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002d19  jmp      0x140003067 ; 
0000000140002d1e  jmp      0x140003067 ; 
0000000140002d23  lea      rax, [rip + 0x15a6] ; [0x1400042d0] 'DWMM'
0000000140002d2a  mov      qword ptr [rsp + 0x28], rax
0000000140002d2f  mov      r9, qword ptr [rsp + 0x38]
0000000140002d34  mov      r8, qword ptr [rsp + 0x30]
0000000140002d39  mov      rdx, qword ptr [rsp + 0x28]
0000000140002d3e  mov      rcx, qword ptr [rsp + 0x40]
0000000140002d43  call     0x140001a98 ; 
0000000140002d48  mov      dword ptr [rsp + 0x20], eax
0000000140002d4c  cmp      dword ptr [rsp + 0x20], 0
0000000140002d51  jge      0x140002d60
0000000140002d53  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002d5b  jmp      0x140003067 ; 
0000000140002d60  jmp      0x140003067 ; 
0000000140002d65  lea      rax, [rip + 0x1574] ; [0x1400042e0] 'DRCP'
0000000140002d6c  mov      qword ptr [rsp + 0x28], rax
0000000140002d71  mov      r9, qword ptr [rsp + 0x38]
0000000140002d76  mov      r8, qword ptr [rsp + 0x30]
0000000140002d7b  mov      rdx, qword ptr [rsp + 0x28]
0000000140002d80  mov      rcx, qword ptr [rsp + 0x40]
0000000140002d85  call     0x140001784 ; 
0000000140002d8a  mov      dword ptr [rsp + 0x20], eax
0000000140002d8e  cmp      dword ptr [rsp + 0x20], 0
0000000140002d93  jge      0x140002da2
0000000140002d95  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002d9d  jmp      0x140003067 ; 
0000000140002da2  jmp      0x140003067 ; 
0000000140002da7  lea      rax, [rip + 0x1542] ; [0x1400042f0] 'DWCP'
0000000140002dae  mov      qword ptr [rsp + 0x28], rax
0000000140002db3  mov      r9, qword ptr [rsp + 0x38]
0000000140002db8  mov      r8, qword ptr [rsp + 0x30]
0000000140002dbd  mov      rdx, qword ptr [rsp + 0x28]
0000000140002dc2  mov      rcx, qword ptr [rsp + 0x40]
0000000140002dc7  call     0x140001a98 ; 
0000000140002dcc  mov      dword ptr [rsp + 0x20], eax
0000000140002dd0  cmp      dword ptr [rsp + 0x20], 0
0000000140002dd5  jge      0x140002de4
0000000140002dd7  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002ddf  jmp      0x140003067 ; 
0000000140002de4  jmp      0x140003067 ; 
0000000140002de9  lea      rax, [rip + 0x1510] ; [0x140004300] 'DROI'
0000000140002df0  mov      qword ptr [rsp + 0x28], rax
0000000140002df5  mov      r9, qword ptr [rsp + 0x38]
0000000140002dfa  mov      r8, qword ptr [rsp + 0x30]
0000000140002dff  mov      rdx, qword ptr [rsp + 0x28]
0000000140002e04  mov      rcx, qword ptr [rsp + 0x40]
0000000140002e09  call     0x140001784 ; 
0000000140002e0e  mov      dword ptr [rsp + 0x20], eax
0000000140002e12  cmp      dword ptr [rsp + 0x20], 0
0000000140002e17  jge      0x140002e26
0000000140002e19  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002e21  jmp      0x140003067 ; 
0000000140002e26  jmp      0x140003067 ; 
0000000140002e2b  lea      rax, [rip + 0x14de] ; [0x140004310] 'DWOI'
0000000140002e32  mov      qword ptr [rsp + 0x28], rax
0000000140002e37  mov      r9, qword ptr [rsp + 0x38]
0000000140002e3c  mov      r8, qword ptr [rsp + 0x30]
0000000140002e41  mov      rdx, qword ptr [rsp + 0x28]
0000000140002e46  mov      rcx, qword ptr [rsp + 0x40]
0000000140002e4b  call     0x140001a98 ; 
0000000140002e50  mov      dword ptr [rsp + 0x20], eax
0000000140002e54  cmp      dword ptr [rsp + 0x20], 0
0000000140002e59  jge      0x140002e68
0000000140002e5b  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002e63  jmp      0x140003067 ; 
0000000140002e68  jmp      0x140003067 ; 
0000000140002e6d  lea      rax, [rip + 0x14ac] ; [0x140004320] 'POIR'
0000000140002e74  mov      qword ptr [rsp + 0x28], rax
0000000140002e79  mov      r9, qword ptr [rsp + 0x38]
0000000140002e7e  mov      r8, qword ptr [rsp + 0x30]
0000000140002e83  mov      rdx, qword ptr [rsp + 0x28]
0000000140002e88  mov      rcx, qword ptr [rsp + 0x40]
0000000140002e8d  call     0x140001a98 ; 
0000000140002e92  mov      dword ptr [rsp + 0x20], eax
0000000140002e96  cmp      dword ptr [rsp + 0x20], 0
0000000140002e9b  jge      0x140002eaa
0000000140002e9d  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002ea5  jmp      0x140003067 ; 
0000000140002eaa  jmp      0x140003067 ; 
0000000140002eaf  lea      rax, [rip + 0x147a] ; [0x140004330] 'POIW'
0000000140002eb6  mov      qword ptr [rsp + 0x28], rax
0000000140002ebb  mov      r9, qword ptr [rsp + 0x38]
0000000140002ec0  mov      r8, qword ptr [rsp + 0x30]
0000000140002ec5  mov      rdx, qword ptr [rsp + 0x28]
0000000140002eca  mov      rcx, qword ptr [rsp + 0x40]
0000000140002ecf  call     0x140001dc0 ; 
0000000140002ed4  mov      dword ptr [rsp + 0x20], eax
0000000140002ed8  cmp      dword ptr [rsp + 0x20], 0
0000000140002edd  jge      0x140002eec
0000000140002edf  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002ee7  jmp      0x140003067 ; 
0000000140002eec  jmp      0x140003067 ; 
0000000140002ef1  lea      rax, [rip + 0x1448] ; [0x140004340] 'DR1T'
0000000140002ef8  mov      qword ptr [rsp + 0x28], rax
0000000140002efd  mov      r9, qword ptr [rsp + 0x38]
0000000140002f02  mov      r8, qword ptr [rsp + 0x30]
0000000140002f07  mov      rdx, qword ptr [rsp + 0x28]
0000000140002f0c  mov      rcx, qword ptr [rsp + 0x40]
0000000140002f11  call     0x140001784 ; 
0000000140002f16  mov      dword ptr [rsp + 0x20], eax
0000000140002f1a  cmp      dword ptr [rsp + 0x20], 0
0000000140002f1f  jge      0x140002f2e
0000000140002f21  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002f29  jmp      0x140003067 ; 
0000000140002f2e  jmp      0x140003067 ; 
0000000140002f33  lea      rax, [rip + 0x1416] ; [0x140004350] 'DR2T'
0000000140002f3a  mov      qword ptr [rsp + 0x28], rax
0000000140002f3f  mov      r9, qword ptr [rsp + 0x38]
0000000140002f44  mov      r8, qword ptr [rsp + 0x30]
0000000140002f49  mov      rdx, qword ptr [rsp + 0x28]
0000000140002f4e  mov      rcx, qword ptr [rsp + 0x40]
0000000140002f53  call     0x140001784 ; 
0000000140002f58  mov      dword ptr [rsp + 0x20], eax
0000000140002f5c  cmp      dword ptr [rsp + 0x20], 0
0000000140002f61  jge      0x140002f70
0000000140002f63  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002f6b  jmp      0x140003067 ; 
0000000140002f70  jmp      0x140003067 ; 
0000000140002f75  lea      rax, [rip + 0x13e4] ; [0x140004360] 'DR3T'
0000000140002f7c  mov      qword ptr [rsp + 0x28], rax
0000000140002f81  mov      r9, qword ptr [rsp + 0x38]
0000000140002f86  mov      r8, qword ptr [rsp + 0x30]
0000000140002f8b  mov      rdx, qword ptr [rsp + 0x28]
0000000140002f90  mov      rcx, qword ptr [rsp + 0x40]
0000000140002f95  call     0x140001784 ; 
0000000140002f9a  mov      dword ptr [rsp + 0x20], eax
0000000140002f9e  cmp      dword ptr [rsp + 0x20], 0
0000000140002fa3  jge      0x140002fb2
0000000140002fa5  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002fad  jmp      0x140003067 ; 
0000000140002fb2  jmp      0x140003067 ; 
0000000140002fb7  lea      rax, [rip + 0x13b2] ; [0x140004370] 'RW1T'
0000000140002fbe  mov      qword ptr [rsp + 0x28], rax
0000000140002fc3  mov      r9, qword ptr [rsp + 0x38]
0000000140002fc8  mov      r8, qword ptr [rsp + 0x30]
0000000140002fcd  mov      rdx, qword ptr [rsp + 0x28]
0000000140002fd2  mov      rcx, qword ptr [rsp + 0x40]
0000000140002fd7  call     0x140001dc0 ; 
0000000140002fdc  mov      dword ptr [rsp + 0x20], eax
0000000140002fe0  cmp      dword ptr [rsp + 0x20], 0
0000000140002fe5  jge      0x140002ff1
0000000140002fe7  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140002fef  jmp      0x140003067 ; 
0000000140002ff1  jmp      0x140003067 ; 
0000000140002ff3  lea      rax, [rip + 0x1386] ; [0x140004380] 'RW2T'
0000000140002ffa  mov      qword ptr [rsp + 0x28], rax
0000000140002fff  mov      r9, qword ptr [rsp + 0x38]
0000000140003004  mov      r8, qword ptr [rsp + 0x30]
0000000140003009  mov      rdx, qword ptr [rsp + 0x28]
000000014000300e  mov      rcx, qword ptr [rsp + 0x40]
0000000140003013  call     0x140001dc0 ; 
0000000140003018  mov      dword ptr [rsp + 0x20], eax
000000014000301c  cmp      dword ptr [rsp + 0x20], 0
0000000140003021  jge      0x14000302d
0000000140003023  mov      dword ptr [rsp + 0x20], 0xc000009a
000000014000302b  jmp      0x140003067 ; 
000000014000302d  jmp      0x140003067 ; 
000000014000302f  lea      rax, [rip + 0x135a] ; [0x140004390] 'RW3T'
0000000140003036  mov      qword ptr [rsp + 0x28], rax
000000014000303b  mov      r9, qword ptr [rsp + 0x38]
0000000140003040  mov      r8, qword ptr [rsp + 0x30]
0000000140003045  mov      rdx, qword ptr [rsp + 0x28]
000000014000304a  mov      rcx, qword ptr [rsp + 0x40]
000000014000304f  call     0x140001dc0 ; 
0000000140003054  mov      dword ptr [rsp + 0x20], eax
0000000140003058  cmp      dword ptr [rsp + 0x20], 0
000000014000305d  jge      0x140003067
000000014000305f  mov      dword ptr [rsp + 0x20], 0xc000009a
0000000140003067  mov      rdx, qword ptr [rsp + 0x490]
000000014000306f  mov      rcx, qword ptr [rsp + 0x488]
0000000140003077  call     0x140003484 ; 
000000014000307c  xor      edx, edx
000000014000307e  mov      rcx, qword ptr [rsp + 0x488]
0000000140003086  call     0x140003370 ; 
000000014000308b  nop      
000000014000308c  mov      rcx, qword ptr [rsp + 0x460]
0000000140003094  xor      rcx, rsp
0000000140003097  call     0x140003580 ; 
000000014000309c  add      rsp, 0x478
00000001400030a3  ret      
