namespace JiYaoChu.Services;

/// <summary>Decodes OEM notifications without polling or changing a device.</summary>
internal static class FirmwareOsdNotices
{
    // Values come from Define/ECSpec.cs constants retained in
    // reverse/native/evidence/oem-constants.json. The event identifier is the
    // low byte of AcpiTest_EventULong. No unverified high-byte payload is used
    // as an on/off state or a brightness/volume value.
    //
    // This model's Fn keys: Esc = Fn lock, F2 = microphone, F4 = airplane,
    // F5 = touchpad, F6/F7 = keyboard light, F8 = mute, F9/F10 = volume,
    // F11/F12 = display brightness. F1 opens the app and F3 controls Win lock
    // in BackgroundHost; ordinary F-key presses are never rebound here.
    internal static bool TryGetNotice(uint code, out string title, out string detail, out string kind)
    {
        var notice = (code & 0xff) switch
        {
            0x04 => ("触摸板", "已开启", "touchpad"), // OSD_TPON
            0x05 => ("触摸板", "已关闭", "touchpad"), // OSD_TPOFF
            0x14 => ("屏幕亮度", "已调高", "brightness"), // OSD_BRIGHTNESSUP
            0x15 => ("屏幕亮度", "已调低", "brightness"), // OSD_BRIGHTNESSDOWN
            0x35 => ("静音", "设置已切换", "volume"), // OSD_MUTE: no state payload verified
            0x36 => ("音量", "已调低", "volume"), // OSD_VOLUMEDOWN
            0x37 => ("音量", "已调高", "volume"), // OSD_VOLUMEUP
            0x3b => ("键盘背光", "已关闭", "keyboard_light"), // OSD_KB_LED_LEVEL0
            >= 0x3c and <= 0x3f => ("键盘背光", $"亮度 {(code & 0xff) - 0x3b} / 4", "keyboard_light"),
            0xa4 => ("飞行模式", "设置已切换", "airplane"), // OSD_AIRPLANEMODE
            0xab => ("供电状态", "电源连接状态已变化", "power"), // OSD_MyBat_ACUpdate
            0xb0 => ("性能与散热", "模式已切换", "performance"), // OSD_FanModeSwitch, not Fn lock
            0xb3 => ("键盘背光", "亮度已调整", "keyboard_light"), // BacklightLevelChange
            0xb4 => ("键盘背光", "设置已切换", "keyboard_light"), // BacklightPowerChange
            0xb7 => ("麦克风", "设置已切换", "microphone"), // TimAP_MicMute_Sw
            0xb8 => ("Fn 锁", "设置已切换", "fn_lock"), // OSD_FnChange
            // 0xC7 is also named PDWarning_Event in OEM constants. Only show
            // a generic notice; Windows power notifications provide the source.
            0xc7 => ("供电状态", "电源状态已变化", "power"),
            // TimAP_ELU_Fn2 does not encode a state. The supplied keyboard
            // layout identifies Fn+F2 as the microphone toggle on this model.
            0xcd => ("麦克风", "设置已切换", "microphone"),
            _ => ("", "", ""),
        };
        (title, detail, kind) = notice;
        return kind.Length != 0;
    }
}
