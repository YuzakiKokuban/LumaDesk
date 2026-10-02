; openrevo.exe SHA256=3e3b9d2297e36a0361d56c0f6357afdd9ebc8b201b943546c5cf8002cfabda09
; preferred image VA 0x14030c6e0..0x14030ca93
000000014030c6e0  push     r14
000000014030c6e2  push     rsi
000000014030c6e3  push     rdi
000000014030c6e4  push     rbx
000000014030c6e5  sub      rsp, 0xc8
000000014030c6ec  mov      word ptr [rsp + 0x4e], cx
000000014030c6f1  mov      byte ptr [rsp + 0x4d], dl
000000014030c6f5  movzx    eax, byte ptr [rip + 0x46cf46] ; [0x140779642] 
000000014030c6fc  test     al, al
000000014030c6fe  je       0x14030c87d
000000014030c704  movzx    eax, byte ptr [rip + 0x46cf38] ; [0x140779643] 
000000014030c70b  test     al, al
000000014030c70d  je       0x14030c88a
000000014030c713  mov      r8d, 0xf
000000014030c719  xor      edx, edx
000000014030c71b  call     0x14050b9d0 ; 
000000014030c720  test     rax, rax
000000014030c723  je       0x14030ca82
000000014030c729  mov      rsi, rax
000000014030c72c  movabs   rax, 0x495043415c2e5c5c
000000014030c736  mov      qword ptr [rsi], rax
000000014030c739  mov      dword ptr [rsi + 8], 0x76697244
000000014030c740  mov      word ptr [rsi + 0xc], 0x7265
000000014030c746  mov      byte ptr [rsi + 0xe], 0
000000014030c74a  mov      qword ptr [rsp + 0x30], 0
000000014030c753  mov      dword ptr [rsp + 0x28], 0
000000014030c75b  mov      dword ptr [rsp + 0x20], 3
000000014030c763  mov      rcx, rsi
000000014030c766  mov      edx, 0xc0000000
000000014030c76b  mov      r8d, 3
000000014030c771  xor      r9d, r9d
000000014030c774  call     qword ptr [rip + 0x2ea046] ; [0x1405f67c0] kernel32.dll!CreateFileA
000000014030c77a  mov      qword ptr [rsp + 0xc0], rax
000000014030c782  lea      rcx, [rax + 1]
000000014030c786  cmp      rcx, 2
000000014030c78a  jae      0x14030c891
000000014030c790  call     qword ptr [rip + 0x2ea09a] ; [0x1405f6830] kernel32.dll!GetLastError
000000014030c796  mov      dword ptr [rsp + 0x50], eax
000000014030c79a  lea      rax, [rsp + 0x4e]
000000014030c79f  mov      qword ptr [rsp + 0x80], rax
000000014030c7a7  lea      rax, [rip - 0x2f3b3e] ; [0x140018c70] 
000000014030c7ae  mov      qword ptr [rsp + 0x88], rax
000000014030c7b6  lea      rax, [rsp + 0x4d]
000000014030c7bb  mov      qword ptr [rsp + 0x90], rax
000000014030c7c3  lea      rax, [rip - 0x2f541a] ; [0x1400173b0] 
000000014030c7ca  mov      qword ptr [rsp + 0x98], rax
000000014030c7d2  lea      rax, [rsp + 0xc0]
000000014030c7da  mov      qword ptr [rsp + 0xa0], rax
000000014030c7e2  lea      rax, [rip - 0x2f25f9] ; [0x14001a1f0] 
000000014030c7e9  mov      qword ptr [rsp + 0xa8], rax
000000014030c7f1  lea      rax, [rsp + 0x50]
000000014030c7f6  mov      qword ptr [rsp + 0xb0], rax
000000014030c7fe  lea      rax, [rip - 0x2f33d5] ; [0x140019430] 
000000014030c805  mov      qword ptr [rsp + 0xb8], rax
000000014030c80d  lea      rdx, [rip + 0x3b18ad] ; [0x1406be0c1] rust-format-first-literal='raw_driver_write_ec(addr='
000000014030c814  lea      rcx, [rsp + 0x58]
000000014030c819  lea      r8, [rsp + 0x80]
000000014030c821  call     0x140003d70 ; 
000000014030c826  mov      rdi, qword ptr [rsp + 0x60]
000000014030c82b  mov      rax, qword ptr [rsp + 0x68]
000000014030c830  mov      rbx, qword ptr [rsp + 0x58]
000000014030c835  mov      qword ptr [rsp + 0x28], rax
000000014030c83a  mov      qword ptr [rsp + 0x20], rdi
000000014030c83f  lea      rcx, [rip + 0x34f636] ; [0x14065be7c] 'ERRORWINDOWexport_device_diagnostic_reportgpuStateset_battery_modemodeecStateget_power_settingsset_cpu_safety_guardset_cpu_freq_limitmhzset_gpu_moderestartget_gpu_mode_infoset_device_switchswi'
000000014030c846  lea      r8, [rip + 0x3b1513] ; [0x1406bdd60] 
000000014030c84d  mov      edx, 5
000000014030c852  mov      r9d, 9
000000014030c858  call     0x1403409f0 ; 
000000014030c85d  test     rbx, rbx
000000014030c860  je       0x14030c876
000000014030c862  call     qword ptr [rip + 0x2e9d90] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030c868  mov      rcx, rax
000000014030c86b  xor      edx, edx
000000014030c86d  mov      r8, rdi
000000014030c870  call     qword ptr [rip + 0x2e9d7a] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014030c876  xor      edi, edi
000000014030c878  jmp      0x14030ca5f ; 
000000014030c87d  call     0x14030ea10 ; 
000000014030c882  test     al, al
000000014030c884  jne      0x14030c713
000000014030c88a  xor      edi, edi
000000014030c88c  jmp      0x14030ca73 ; 
000000014030c891  movzx    ecx, word ptr [rsp + 0x4e]
000000014030c896  movzx    edx, byte ptr [rsp + 0x4d]
000000014030c89b  mov      dword ptr [rsp + 0x50], ecx
000000014030c89f  mov      dword ptr [rsp + 0x54], edx
000000014030c8a3  mov      dword ptr [rsp + 0x70], 0
000000014030c8ab  mov      dword ptr [rsp + 0x74], 0
000000014030c8b3  lea      rcx, [rsp + 0x74]
000000014030c8b8  mov      qword ptr [rsp + 0x30], rcx
000000014030c8bd  lea      rcx, [rsp + 0x70]
000000014030c8c2  mov      qword ptr [rsp + 0x20], rcx
000000014030c8c7  mov      qword ptr [rsp + 0x38], 0
000000014030c8d0  mov      dword ptr [rsp + 0x28], 4
000000014030c8d8  lea      r8, [rsp + 0x50]
000000014030c8dd  mov      rcx, rax
000000014030c8e0  mov      edx, 0x9c40a48c
000000014030c8e5  mov      r9d, 8
000000014030c8eb  mov      rdi, rax
000000014030c8ee  call     qword ptr [rip + 0x2e9ec4] ; [0x1405f67b8] kernel32.dll!DeviceIoControl
000000014030c8f4  mov      ebx, eax
000000014030c8f6  mov      dword ptr [rsp + 0x78], eax
000000014030c8fa  call     qword ptr [rip + 0x2e9f30] ; [0x1405f6830] kernel32.dll!GetLastError
000000014030c900  mov      dword ptr [rsp + 0x7c], eax
000000014030c904  mov      rcx, rdi
000000014030c907  call     qword ptr [rip + 0x2e9ccb] ; [0x1405f65d8] kernel32.dll!CloseHandle
000000014030c90d  test     ebx, ebx
000000014030c90f  setne    dil
000000014030c913  lea      rax, [rsp + 0x4e]
000000014030c918  je       0x14030c98b
000000014030c91a  mov      qword ptr [rsp + 0x80], rax
000000014030c922  lea      rax, [rip - 0x2f3cb9] ; [0x140018c70] 
000000014030c929  mov      qword ptr [rsp + 0x88], rax
000000014030c931  lea      rax, [rsp + 0x4d]
000000014030c936  mov      qword ptr [rsp + 0x90], rax
000000014030c93e  lea      rax, [rip - 0x2f5595] ; [0x1400173b0] 
000000014030c945  mov      qword ptr [rsp + 0x98], rax
000000014030c94d  lea      rdx, [rip + 0x3b1810] ; [0x1406be164] rust-format-first-literal='raw_driver_write_ec(addr='
000000014030c954  lea      rcx, [rsp + 0x58]
000000014030c959  lea      r8, [rsp + 0x80]
000000014030c961  call     0x140003d70 ; 
000000014030c966  mov      rbx, qword ptr [rsp + 0x60]
000000014030c96b  mov      rax, qword ptr [rsp + 0x68]
000000014030c970  mov      r14, qword ptr [rsp + 0x58]
000000014030c975  mov      qword ptr [rsp + 0x28], rax
000000014030c97a  mov      qword ptr [rsp + 0x20], rbx
000000014030c97f  lea      rcx, [rip + 0x351bd3] ; [0x14065e559] 
000000014030c986  jmp      0x14030ca2f ; 
000000014030c98b  mov      qword ptr [rsp + 0x80], rax
000000014030c993  lea      rax, [rip - 0x2f3d2a] ; [0x140018c70] 
000000014030c99a  mov      qword ptr [rsp + 0x88], rax
000000014030c9a2  lea      rax, [rsp + 0x4d]
000000014030c9a7  mov      qword ptr [rsp + 0x90], rax
000000014030c9af  lea      rax, [rip - 0x2f5606] ; [0x1400173b0] 
000000014030c9b6  mov      qword ptr [rsp + 0x98], rax
000000014030c9be  lea      rax, [rsp + 0x78]
000000014030c9c3  mov      qword ptr [rsp + 0xa0], rax
000000014030c9cb  lea      rax, [rip - 0x2f3492] ; [0x140019540] 
000000014030c9d2  mov      qword ptr [rsp + 0xa8], rax
000000014030c9da  lea      rax, [rsp + 0x7c]
000000014030c9df  mov      qword ptr [rsp + 0xb0], rax
000000014030c9e7  lea      rax, [rip - 0x2f35be] ; [0x140019430] 
000000014030c9ee  mov      qword ptr [rsp + 0xb8], rax
000000014030c9f6  lea      rdx, [rip + 0x3b1713] ; [0x1406be110] rust-format-first-literal='raw_driver_write_ec(addr='
000000014030c9fd  lea      rcx, [rsp + 0x58]
000000014030ca02  lea      r8, [rsp + 0x80]
000000014030ca0a  call     0x140003d70 ; 
000000014030ca0f  mov      rbx, qword ptr [rsp + 0x60]
000000014030ca14  mov      rax, qword ptr [rsp + 0x68]
000000014030ca19  mov      r14, qword ptr [rsp + 0x58]
000000014030ca1e  mov      qword ptr [rsp + 0x28], rax
000000014030ca23  mov      qword ptr [rsp + 0x20], rbx
000000014030ca28  lea      rcx, [rip + 0x34f44d] ; [0x14065be7c] 'ERRORWINDOWexport_device_diagnostic_reportgpuStateset_battery_modemodeecStateget_power_settingsset_cpu_safety_guardset_cpu_freq_limitmhzset_gpu_moderestartget_gpu_mode_infoset_device_switchswi'
000000014030ca2f  lea      r8, [rip + 0x3b132a] ; [0x1406bdd60] 
000000014030ca36  mov      edx, 5
000000014030ca3b  mov      r9d, 9
000000014030ca41  call     0x1403409f0 ; 
000000014030ca46  test     r14, r14
000000014030ca49  je       0x14030ca5f
000000014030ca4b  call     qword ptr [rip + 0x2e9ba7] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030ca51  mov      rcx, rax
000000014030ca54  xor      edx, edx
000000014030ca56  mov      r8, rbx
000000014030ca59  call     qword ptr [rip + 0x2e9b91] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014030ca5f  call     qword ptr [rip + 0x2e9b93] ; [0x1405f65f8] kernel32.dll!GetProcessHeap
000000014030ca65  mov      rcx, rax
000000014030ca68  xor      edx, edx
000000014030ca6a  mov      r8, rsi
000000014030ca6d  call     qword ptr [rip + 0x2e9b7d] ; [0x1405f65f0] kernel32.dll!HeapFree
000000014030ca73  mov      eax, edi
000000014030ca75  add      rsp, 0xc8
000000014030ca7c  pop      rbx
000000014030ca7d  pop      rdi
000000014030ca7e  pop      rsi
000000014030ca7f  pop      r14
000000014030ca81  ret      
000000014030ca82  mov      ecx, 1
000000014030ca87  mov      edx, 0xf
000000014030ca8c  call     0x1405cb7af ; 
000000014030ca91  ud2      
