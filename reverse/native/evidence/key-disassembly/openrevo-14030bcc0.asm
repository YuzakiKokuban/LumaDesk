; openrevo.exe SHA256=3e3b9d2297e36a0361d56c0f6357afdd9ebc8b201b943546c5cf8002cfabda09
; preferred image VA 0x14030bcc0..0x14030bd81
000000014030bcc0  push     rsi
000000014030bcc1  push     rbx
000000014030bcc2  sub      rsp, 0x38
000000014030bcc6  mov      ebx, ecx
000000014030bcc8  mov      cx, 0x767
000000014030bccc  call     0x14030ae10 ; 
000000014030bcd1  movzx    ecx, dl
000000014030bcd4  xor      edx, edx
000000014030bcd6  test     al, 1
000000014030bcd8  cmovne   edx, ecx
000000014030bcdb  mov      eax, dword ptr [rip + 0x46a6c7] ; [0x1407763a8] 
000000014030bce1  test     eax, eax
000000014030bce3  jne      0x14030bd17
000000014030bce5  or       dl, bl
000000014030bce7  mov      cl, 1
000000014030bce9  xor      eax, eax
000000014030bceb  lock cmpxchg byte ptr [rip + 0x46a6ad], cl ; [0x1407763a0] 
000000014030bcf3  jne      0x14030bd59
000000014030bcf5  mov      cx, 0x767
000000014030bcf9  call     0x14030c6e0 ; 
000000014030bcfe  mov      ebx, eax
000000014030bd00  xor      ecx, ecx
000000014030bd02  mov      al, 1
000000014030bd04  lock cmpxchg byte ptr [rip + 0x46a694], cl ; [0x1407763a0] 
000000014030bd0c  jne      0x14030bd71
000000014030bd0e  mov      eax, ebx
000000014030bd10  add      rsp, 0x38
000000014030bd14  pop      rbx
000000014030bd15  pop      rsi
000000014030bd16  ret      
000000014030bd17  lea      rax, [rip + 0x46a682] ; [0x1407763a0] 
000000014030bd1e  mov      qword ptr [rsp + 0x28], rax
000000014030bd23  lea      rax, [rsp + 0x28]
000000014030bd28  mov      qword ptr [rsp + 0x30], rax
000000014030bd2d  lea      rax, [rip + 0x33e354] ; [0x14064a088] rust-pieces=['/rustc/88d9e12ae178fab0fb5cc050a94da85685d449ea/library\\std\\src\\sync\\once.rs']
000000014030bd34  mov      qword ptr [rsp + 0x20], rax
000000014030bd39  lea      rcx, [rip + 0x46a668] ; [0x1407763a8] 
000000014030bd40  lea      r9, [rip + 0x33e6f1] ; [0x14064a438] 
000000014030bd47  lea      r8, [rsp + 0x30]
000000014030bd4c  mov      esi, edx
000000014030bd4e  mov      dl, 1
000000014030bd50  call     0x1405ec5e0 ; 
000000014030bd55  mov      edx, esi
000000014030bd57  jmp      0x14030bce5 ; 
000000014030bd59  lea      rcx, [rip + 0x46a640] ; [0x1407763a0] 
000000014030bd60  mov      r8d, 0xffffffff
000000014030bd66  mov      esi, edx
000000014030bd68  call     0x1405df9a0 ; 
000000014030bd6d  mov      edx, esi
000000014030bd6f  jmp      0x14030bcf5 ; 
000000014030bd71  lea      rcx, [rip + 0x46a628] ; [0x1407763a0] 
000000014030bd78  xor      edx, edx
000000014030bd7a  call     0x1405df6d0 ; 
000000014030bd7f  jmp      0x14030bd0e ; 
