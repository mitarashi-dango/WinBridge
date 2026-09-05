using System.Globalization;

namespace WinBridge.Services;

internal static class CatalogTranslationService
{
    private sealed record Term(string Spanish, string SimplifiedChinese, string TraditionalChinese);

    private static readonly IReadOnlyDictionary<string, Term> Categories =
        new Dictionary<string, Term>(StringComparer.OrdinalIgnoreCase)
        {
            ["system"] = new("Sistema", "系统", "系統"),
            ["devices"] = new("Dispositivos", "设备", "裝置"),
            ["network"] = new("Red e Internet", "网络和 Internet", "網路和網際網路"),
            ["personalization"] = new("Personalización", "个性化", "個人化"),
            ["apps"] = new("Aplicaciones", "应用", "應用程式"),
            ["accounts"] = new("Cuentas", "账户", "帳戶"),
            ["time"] = new("Hora e idioma", "时间和语言", "時間與語言"),
            ["gaming"] = new("Juegos", "游戏", "遊戲"),
            ["accessibility"] = new("Accesibilidad", "辅助功能", "協助工具"),
            ["privacy"] = new("Privacidad y seguridad", "隐私和安全性", "隱私權與安全性"),
            ["update"] = new("Windows Update", "Windows 更新", "Windows Update"),
            ["family"] = new("Cuentas", "账户", "帳戶"),
            ["search"] = new("Búsqueda", "搜索", "搜尋"),
            ["sound"] = new("Sonido", "声音", "音效")
        };

    private static readonly IReadOnlyDictionary<string, Term> ExactNames =
        new Dictionary<string, Term>(StringComparer.OrdinalIgnoreCase)
        {
            ["volume-mixer"] = new("Mezclador de volumen", "音量混合器", "音量混音程式"),
            ["battery-saver"] = new("Ahorro de batería", "节电模式", "省電模式"),
            ["battery-saver-settings"] = new("Configuración de ahorro de batería", "节电模式设置", "省電模式設定"),
            ["battery-usage"] = new("Uso de la batería", "电池使用情况", "電池使用量"),
            ["energy-recommendations"] = new("Recomendaciones de energía", "能源建议", "能源建議"),
            ["focus-assist"] = new("Concentración", "专注", "專注"),
            ["graphics-defaults"] = new("Configuración gráfica predeterminada", "默认图形设置", "預設圖形設定"),
            ["night-light"] = new("Luz nocturna", "夜间模式", "夜間光線"),
            ["remote-desktop"] = new("Escritorio remoto", "远程桌面", "遠端桌面"),
            ["storage-sense"] = new("Sensor de almacenamiento", "存储感知", "儲存空間感知器"),
            ["storage-recommendations"] = new("Recomendaciones de almacenamiento", "存储建议", "儲存空間建議"),
            ["disks-volumes"] = new("Discos y volúmenes", "磁盘和卷", "磁碟與磁碟區"),
            ["default-output"] = new("Salida de audio predeterminada", "默认音频输出", "預設音訊輸出"),
            ["text-suggestions"] = new("Sugerencias de texto del teclado físico", "硬件键盘文本建议", "硬體鍵盤文字建議"),
            ["hearing-devices"] = new("Dispositivos auditivos", "听力设备", "聽力裝置"),
            ["color-filters"] = new("Filtros de color", "颜色筛选器", "色彩篩選"),
            ["eye-control"] = new("Control ocular", "眼动控制", "眼球控制"),
            ["high-contrast"] = new("Temas de contraste", "对比度主题", "對比主題"),
            ["visual-effects"] = new("Efectos visuales", "视觉效果", "視覺效果"),
            ["text-cursor"] = new("Cursor de texto", "文本光标", "文字游標"),
            ["game-bar"] = new("Barra de juegos", "游戏栏", "遊戲列"),
            ["known-wifi"] = new("Administrar redes conocidas", "管理已知网络", "管理已知網路"),
            ["start-folders"] = new("Carpetas de Inicio", "开始菜单文件夹", "開始功能表資料夾"),
            ["touch-keyboard"] = new("Teclado táctil", "触摸键盘", "觸控式鍵盤"),
            ["dynamic-lighting"] = new("Iluminación dinámica", "动态照明", "動態光效"),
            ["copilot-key"] = new("Tecla Copilot", "Copilot 键", "Copilot 鍵"),
            ["device-usage"] = new("Uso del dispositivo", "设备使用情况", "裝置使用方式"),
            ["file-system"] = new("Permisos del sistema de archivos", "文件系统权限", "檔案系統權限"),
            ["account-info"] = new("Permisos de información de la cuenta", "账户信息权限", "帳戶資訊權限"),
            ["activity-history"] = new("Historial de actividad", "活动历史记录", "活動歷程記錄"),
            ["app-diagnostics"] = new("Diagnósticos de aplicaciones", "应用诊断", "應用程式診斷"),
            ["auto-downloads"] = new("Descargas automáticas de archivos", "自动文件下载", "自動下載檔案"),
            ["call-history"] = new("Permisos del historial de llamadas", "通话记录权限", "通話記錄權限"),
            ["other-devices"] = new("Permisos de otros dispositivos", "其他设备权限", "其他裝置權限"),
            ["eye-tracker"] = new("Permisos de seguimiento ocular", "眼动跟踪权限", "眼球追蹤權限"),
            ["graphics-border"] = new("Permisos de bordes de captura", "屏幕截图边框权限", "螢幕擷取畫面框線權限"),
            ["graphics-capture"] = new("Permisos de captura de pantalla", "屏幕捕获权限", "螢幕擷取權限"),
            ["inking-typing"] = new("Personalización de escritura y entrada", "墨迹书写和键入个性化", "手寫輸入與輸入個人化"),
            ["phone-calls"] = new("Permisos de llamadas telefónicas", "电话权限", "電話權限"),
            ["voice-activation"] = new("Activación por voz", "语音激活", "語音啟用"),
            ["save-locations"] = new("Ubicaciones para guardar contenido nuevo", "新内容保存位置", "新內容儲存位置"),
            ["advanced-display"] = new("Pantalla avanzada", "高级显示设置", "進階顯示設定"),
            ["keyboard-advanced"] = new("Configuración avanzada del teclado", "高级键盘设置", "進階鍵盤設定"),
            ["japanese-ime"] = new("IME japonés", "日语输入法", "日文輸入法"),
            ["delivery-activity"] = new("Actividad de Optimización de distribución", "传递优化活动", "傳遞最佳化活動"),
            ["delivery-advanced"] = new("Opciones avanzadas de Optimización de distribución", "传递优化高级选项", "傳遞最佳化進階選項"),
            ["find-device"] = new("Buscar mi dispositivo", "查找我的设备", "尋找我的裝置"),
            ["active-hours"] = new("Horas activas", "使用时段", "使用時間"),
            ["restart-options"] = new("Opciones de reinicio", "重启选项", "重新啟動選項"),
            ["hello-face"] = new("Reconocimiento facial de Windows Hello", "Windows Hello 人脸识别", "Windows Hello 臉部辨識"),
            ["hello-fingerprint"] = new("Huella digital de Windows Hello", "Windows Hello 指纹识别", "Windows Hello 指紋辨識"),
            ["security-key"] = new("Clave de seguridad", "安全密钥", "安全性金鑰")
        };

    private static readonly IReadOnlyDictionary<string, Term> Words =
        new Dictionary<string, Term>(StringComparer.OrdinalIgnoreCase)
        {
            ["about"] = new("Acerca de", "关于", "關於"),
            ["account"] = new("cuenta", "账户", "帳戶"),
            ["activation"] = new("activación", "激活", "啟用"),
            ["active"] = new("activas", "活动", "使用中"),
            ["activity"] = new("actividad", "活动", "活動"),
            ["advanced"] = new("avanzada", "高级", "進階"),
            ["airplane"] = new("avión", "飞行", "飛航"),
            ["app"] = new("aplicación", "应用", "應用程式"),
            ["assist"] = new("asistencia", "助手", "輔助"),
            ["audio"] = new("audio", "音频", "音訊"),
            ["auto"] = new("automática", "自动", "自動"),
            ["autoplay"] = new("reproducción automática", "自动播放", "自動播放"),
            ["background"] = new("fondo", "背景", "背景"),
            ["bar"] = new("barra", "栏", "列"),
            ["battery"] = new("batería", "电池", "電池"),
            ["bluetooth"] = new("Bluetooth", "蓝牙", "藍牙"),
            ["border"] = new("borde", "边框", "框線"),
            ["calendar"] = new("calendario", "日历", "行事曆"),
            ["call"] = new("llamada", "通话", "通話"),
            ["calls"] = new("llamadas", "电话", "電話"),
            ["camera"] = new("cámara", "相机", "相機"),
            ["captions"] = new("subtítulos", "字幕", "輔助字幕"),
            ["capture"] = new("captura", "捕获", "擷取"),
            ["captures"] = new("capturas", "捕获", "擷取"),
            ["cellular"] = new("red móvil", "手机网络", "行動數據"),
            ["center"] = new("centro", "中心", "中心"),
            ["clipboard"] = new("portapapeles", "剪贴板", "剪貼簿"),
            ["color"] = new("color", "颜色", "色彩"),
            ["colors"] = new("colores", "颜色", "色彩"),
            ["connected"] = new("dispositivos conectados", "已连接设备", "已連線裝置"),
            ["contacts"] = new("contactos", "联系人", "連絡人"),
            ["contrast"] = new("contraste", "对比度", "對比"),
            ["control"] = new("control", "控制", "控制"),
            ["copilot"] = new("Copilot", "Copilot", "Copilot"),
            ["cursor"] = new("cursor", "光标", "游標"),
            ["datetime"] = new("fecha y hora", "日期和时间", "日期與時間"),
            ["default"] = new("predeterminada", "默认", "預設"),
            ["defaults"] = new("predeterminadas", "默认", "預設"),
            ["delivery"] = new("optimización de distribución", "传递优化", "傳遞最佳化"),
            ["desktop"] = new("escritorio", "桌面", "桌面"),
            ["details"] = new("detalles", "详细信息", "詳細資料"),
            ["developers"] = new("para desarrolladores", "开发者选项", "開發人員選項"),
            ["device"] = new("dispositivo", "设备", "裝置"),
            ["devices"] = new("dispositivos", "设备", "裝置"),
            ["diagnostics"] = new("diagnósticos", "诊断", "診斷"),
            ["dial"] = new("Dial", "拨号盘", "Dial"),
            ["dialup"] = new("acceso telefónico", "拨号", "撥號"),
            ["directaccess"] = new("DirectAccess", "DirectAccess", "DirectAccess"),
            ["disks"] = new("discos", "磁盘", "磁碟"),
            ["display"] = new("pantalla", "显示", "顯示"),
            ["documents"] = new("documentos", "文档", "文件"),
            ["downloads"] = new("descargas", "下载", "下載"),
            ["dynamic"] = new("dinámica", "动态", "動態"),
            ["effects"] = new("efectos", "效果", "效果"),
            ["email"] = new("correo electrónico", "电子邮件", "電子郵件"),
            ["encryption"] = new("cifrado", "加密", "加密"),
            ["energy"] = new("energía", "能源", "能源"),
            ["ethernet"] = new("Ethernet", "以太网", "乙太網路"),
            ["experiences"] = new("experiencias", "体验", "體驗"),
            ["eye"] = new("ocular", "眼动", "眼球"),
            ["face"] = new("facial", "人脸", "臉部"),
            ["features"] = new("características", "功能", "功能"),
            ["feedback"] = new("comentarios", "反馈", "意見反應"),
            ["file"] = new("archivo", "文件", "檔案"),
            ["filters"] = new("filtros", "筛选器", "篩選"),
            ["find"] = new("buscar", "查找", "尋找"),
            ["fingerprint"] = new("huella digital", "指纹", "指紋"),
            ["focus"] = new("concentración", "专注", "專注"),
            ["folders"] = new("carpetas", "文件夹", "資料夾"),
            ["fonts"] = new("fuentes", "字体", "字型"),
            ["fullscreen"] = new("pantalla completa", "全屏", "全螢幕"),
            ["game"] = new("juego", "游戏", "遊戲"),
            ["general"] = new("general", "常规", "一般"),
            ["graphics"] = new("gráficos", "图形", "圖形"),
            ["group"] = new("familia", "家庭", "家庭"),
            ["hearing"] = new("audición", "听力", "聽力"),
            ["hello"] = new("Windows Hello", "Windows Hello", "Windows Hello"),
            ["high"] = new("alto", "高", "高"),
            ["history"] = new("historial", "历史记录", "歷程記錄"),
            ["hotspot"] = new("zona con cobertura inalámbrica", "移动热点", "行動熱點"),
            ["hours"] = new("horas", "时段", "時間"),
            ["ime"] = new("IME", "输入法", "輸入法"),
            ["info"] = new("tu información", "用户信息", "您的資訊"),
            ["inking"] = new("escritura", "墨迹书写", "手寫輸入"),
            ["input"] = new("entrada", "输入", "輸入"),
            ["insider"] = new("Windows Insider Program", "Windows 预览体验计划", "Windows 測試人員計畫"),
            ["installed"] = new("aplicaciones instaladas", "已安装应用", "已安裝的應用程式"),
            ["japanese"] = new("japonés", "日语", "日文"),
            ["key"] = new("tecla", "键", "鍵"),
            ["keyboard"] = new("teclado", "键盘", "鍵盤"),
            ["kiosk"] = new("quiosco", "展台", "Kiosk"),
            ["known"] = new("conocidas", "已知", "已知"),
            ["language"] = new("idioma", "语言", "語言"),
            ["light"] = new("luz", "灯光", "燈光"),
            ["lighting"] = new("iluminación", "照明", "光效"),
            ["location"] = new("ubicación", "位置", "位置"),
            ["locations"] = new("ubicaciones", "位置", "位置"),
            ["lock"] = new("bloqueo", "锁定", "鎖定"),
            ["lockscreen"] = new("pantalla de bloqueo", "锁屏界面", "鎖定畫面"),
            ["magnifier"] = new("lupa", "放大镜", "放大鏡"),
            ["main"] = new("información general", "概述", "概觀"),
            ["messaging"] = new("mensajería", "消息", "訊息"),
            ["microphone"] = new("micrófono", "麦克风", "麥克風"),
            ["mixer"] = new("mezclador", "混合器", "混音程式"),
            ["mobile"] = new("móvil", "移动设备", "行動裝置"),
            ["mode"] = new("modo", "模式", "模式"),
            ["motion"] = new("movimiento", "运动", "動作"),
            ["mouse"] = new("ratón", "鼠标", "滑鼠"),
            ["multitasking"] = new("multitarea", "多任务处理", "多工"),
            ["music"] = new("música", "音乐", "音樂"),
            ["narrator"] = new("Narrador", "讲述人", "朗讀程式"),
            ["night"] = new("nocturna", "夜间", "夜間"),
            ["notifications"] = new("notificaciones", "通知", "通知"),
            ["optional"] = new("opcionales", "可选更新", "選用更新"),
            ["options"] = new("opciones", "选项", "選項"),
            ["other"] = new("otros", "其他", "其他"),
            ["output"] = new("salida", "输出", "輸出"),
            ["pen"] = new("lápiz y Windows Ink", "笔和 Windows Ink", "手寫筆與 Windows Ink"),
            ["permissions"] = new("permisos", "权限", "權限"),
            ["phone"] = new("teléfono", "电话", "電話"),
            ["pictures"] = new("imágenes", "图片", "圖片"),
            ["playback"] = new("reproducción", "播放", "播放"),
            ["pointer"] = new("puntero", "指针", "指標"),
            ["power"] = new("energía", "电源", "電源"),
            ["presence"] = new("detección de presencia", "存在感应", "顯示狀態偵測"),
            ["printers"] = new("impresoras y escáneres", "打印机和扫描仪", "印表機與掃描器"),
            ["projecting"] = new("proyección en este PC", "投影到此电脑", "投影到此電腦"),
            ["proxy"] = new("proxy", "代理", "Proxy"),
            ["radios"] = new("radios", "无线通信", "無線電"),
            ["recommendations"] = new("recomendaciones", "建议", "建議"),
            ["recovery"] = new("recuperación", "恢复", "復原"),
            ["region"] = new("región", "区域", "地區"),
            ["remote"] = new("remoto", "远程", "遠端"),
            ["restart"] = new("reinicio", "重启", "重新啟動"),
            ["save"] = new("guardar", "保存", "儲存"),
            ["saver"] = new("ahorro", "节电", "省電"),
            ["security"] = new("seguridad", "安全性", "安全性"),
            ["sense"] = new("sensor", "感知", "感知器"),
            ["settings"] = new("configuración", "设置", "設定"),
            ["shared"] = new("compartidas", "共享", "共用"),
            ["signin"] = new("opciones de inicio de sesión", "登录选项", "登入選項"),
            ["sound"] = new("sonido", "声音", "音效"),
            ["speech"] = new("voz", "语音", "語音"),
            ["start"] = new("Inicio", "开始", "開始"),
            ["startup"] = new("aplicaciones de inicio", "启动应用", "啟動應用程式"),
            ["status"] = new("estado", "状态", "狀態"),
            ["storage"] = new("almacenamiento", "存储", "儲存空間"),
            ["suggestions"] = new("sugerencias", "建议", "建議"),
            ["surface"] = new("Surface", "Surface", "Surface"),
            ["sync"] = new("sincronización de Windows", "Windows 设置同步", "Windows 設定同步"),
            ["system"] = new("sistema", "系统", "系統"),
            ["taskbar"] = new("barra de tareas", "任务栏", "工作列"),
            ["tasks"] = new("tareas", "任务", "工作"),
            ["text"] = new("texto", "文本", "文字"),
            ["themes"] = new("temas", "主题", "佈景主題"),
            ["touch"] = new("entrada táctil", "触摸", "觸控"),
            ["touchpad"] = new("panel táctil", "触摸板", "觸控板"),
            ["tracker"] = new("seguimiento", "跟踪", "追蹤"),
            ["troubleshoot"] = new("solucionar problemas", "疑难解答", "疑難排解"),
            ["typing"] = new("escritura", "键入", "輸入"),
            ["usage"] = new("uso", "使用情况", "使用量"),
            ["usb"] = new("USB", "USB", "USB"),
            ["users"] = new("usuarios", "用户", "使用者"),
            ["video"] = new("vídeo", "视频", "視訊"),
            ["videos"] = new("vídeos", "视频", "視訊"),
            ["visual"] = new("visuales", "视觉", "視覺"),
            ["voice"] = new("voz", "语音", "語音"),
            ["volume"] = new("volumen", "音量", "音量"),
            ["volumes"] = new("volúmenes", "卷", "磁碟區"),
            ["vpn"] = new("VPN", "VPN", "VPN"),
            ["websites"] = new("aplicaciones para sitios web", "网站应用", "網站應用程式"),
            ["wifi"] = new("Wi-Fi", "Wi-Fi", "Wi-Fi"),
            ["workplace"] = new("acceso al trabajo o la escuela", "访问工作或学校", "存取公司或學校")
        };

    public static string GetModuleDescription(string id, string language) =>
        (id, language) switch
        {
            ("power", "es-ES") => "Consulta y cambia los tiempos de apagado de pantalla y suspensión.",
            ("windows-update", "es-ES") => "Abre páginas oficiales de Windows para actualizaciones, historial y reinicios.",
            ("search", "es-ES") => "Consulta la configuración de búsqueda y soluciones para problemas habituales.",
            ("explorer", "es-ES") => "Administra la visualización de archivos y reinicia el Explorador de forma segura.",
            ("devices", "es-ES") => "Revisa el estado de los dispositivos y reúne la configuración que utilizas.",
            ("power", "zh-CN") => "查看和更改屏幕关闭及睡眠计时。",
            ("windows-update", "zh-CN") => "打开更新、历史记录和重启设置的 Windows 官方页面。",
            ("search", "zh-CN") => "查看搜索设置和常见问题的安全处理方法。",
            ("explorer", "zh-CN") => "管理文件显示选项并安全重启文件资源管理器。",
            ("devices", "zh-CN") => "查看设备状态并汇集常用的设备设置。",
            ("power", "zh-TW") => "檢視及變更螢幕關閉與睡眠計時。",
            ("windows-update", "zh-TW") => "開啟更新、歷程記錄與重新啟動設定的 Windows 官方頁面。",
            ("search", "zh-TW") => "檢視搜尋設定及常見問題的安全處理方式。",
            ("explorer", "zh-TW") => "管理檔案顯示選項並安全地重新啟動檔案總管。",
            ("devices", "zh-TW") => "檢視裝置狀態並彙整常用的裝置設定。",
            _ => ""
        };

    public static string GetCategory(string id, string language)
    {
        var prefix = id.Split('.', 2)[0];
        return Categories.TryGetValue(prefix, out var term)
            ? Select(term, language)
            : language switch
            {
                "es-ES" => "Configuración de Windows",
                "zh-CN" => "Windows 设置",
                "zh-TW" => "Windows 設定",
                _ => "Windows settings"
            };
    }

    public static string GetSettingName(string id, string language)
    {
        var part = id.Contains('.') ? id[(id.IndexOf('.') + 1)..] : id;
        if (ExactNames.TryGetValue(part, out var exact)) return Select(exact, language);

        var words = part.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var translated = words.Select(word =>
            Words.TryGetValue(word, out var term) ? Select(term, language) : word);
        var result = language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? string.Concat(translated)
            : string.Join(" ", translated);
        if (language != "es-ES" || result.Length == 0) return result;
        return char.ToUpper(result[0], CultureInfo.GetCultureInfo("es-ES")) + result[1..];
    }

    public static string GetDescription(string displayName, string language) =>
        language switch
        {
            "es-ES" => $"Abre la página de configuración de Windows para {displayName}.",
            "zh-CN" => $"打开“{displayName}”的 Windows 设置页面。",
            "zh-TW" => $"開啟「{displayName}」的 Windows 設定頁面。",
            _ => $"Open the Windows settings page for {displayName}."
        };

    private static string Select(Term term, string language) =>
        language switch
        {
            "es-ES" => term.Spanish,
            "zh-CN" => term.SimplifiedChinese,
            "zh-TW" => term.TraditionalChinese,
            _ => term.Spanish
        };
}
