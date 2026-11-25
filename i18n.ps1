# Sleepify - shared UI strings for the PowerShell side (start.ps1 / uninstall.ps1)
# Language resolution: config\lang.txt  ->  OS UI language  ->  English

function Get-SleepifyLang {
    # 1) explicit choice, written by the installer
    try {
        $p = Join-Path $PSScriptRoot 'config\lang.txt'
        if (Test-Path $p) {
            $v = (Get-Content $p -TotalCount 1 -ErrorAction SilentlyContinue)
            if ($v) {
                $v = $v.Trim().ToLowerInvariant()
                if ($v) { return $v }
            }
        }
    } catch { }

    # 2) Windows display language, then installed UI culture, then system locale.
    #    NOTE: CultureInfo::CurrentUICulture is the *PowerShell* UI culture, which is
    #    en-US on an English PowerShell even on a Chinese Windows - do not use it.
    $cands = @()
    try {
        $pref = (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name PreferredUILanguages -ErrorAction SilentlyContinue).PreferredUILanguages
        if ($pref) { foreach ($x in @($pref)) { if ($x) { $cands += [string]$x } } }
    } catch { }
    try { $cands += [string][System.Globalization.CultureInfo]::InstalledUICulture.Name } catch { }
    try { $cands += [string](Get-WinSystemLocale).Name } catch { }

    foreach ($c in $cands) {
        if (-not $c) { continue }
        $two = ($c -split '-')[0].ToLowerInvariant()
        if ($script:SleepifyStrings.ContainsKey($two)) { return $two }
    }
    return 'en'
}

$script:SleepifyStrings = @{

    'zh' = @{
        startTitle = 'Sleepify - 高风险平台'
        startBody  = "此工具会大幅拉低 CPU 使用率，并长时间维持低负载、低电压状态。`r`n`r`n对 AMD Ryzen 5000 / 6000 / 7000 移动端标压（H / HS / HX）而言，低负载低压正是硅片退化后 SoC（尤其内存控制器）失稳甚至崩溃的工况，可能造成毁灭性的稳定性影响。`r`n`r`n本机 CPU：`r`n{CPU}`r`n`r`n是否仍要继续？"
        finalTitle = 'Sleepify'
        finalBody  = "此应用所做更改均已恢复。`r`n`r`n接下来您可以安全地删除此应用目录下的所有内容。`r`n如您暂时不想删除，也可以随时双击 start.cmd 重新部署服务。"
        btnDelete  = '一键删除'
        btnClose   = '关闭'
    }

    'en' = @{
        startTitle = 'Sleepify - Risky platform'
        startBody  = "This tool forces a very low CPU load for hours.`r`n`r`nOn AMD Ryzen 5000 / 6000 / 7000 mobile H-series (H / HS / HX), low load and low voltage are exactly where a degraded SoC - the memory controller in particular - becomes unstable or crashes.`r`n`r`nDetected CPU:`r`n{CPU}`r`n`r`nContinue anyway?"
        finalTitle = 'Sleepify'
        finalBody  = "All changes made by this application have been reverted.`r`n`r`nYou can now safely delete everything in this folder.`r`nIf you prefer to keep it, run start.cmd again at any time to redeploy the service."
        btnDelete  = 'Delete all'
        btnClose   = 'Close'
    }

    'fr' = @{
        startTitle = 'Sleepify - Plateforme à risque'
        startBody  = "Cet outil impose une charge CPU très faible pendant des heures.`r`n`r`nSur les AMD Ryzen 5000 / 6000 / 7000 mobiles série H (H / HS / HX), une charge et une tension faibles sont précisément le régime où un SoC dégradé - le contrôleur mémoire en particulier - devient instable ou plante.`r`n`r`nProcesseur détecté :`r`n{CPU}`r`n`r`nContinuer quand même ?"
        finalTitle = 'Sleepify'
        finalBody  = "Toutes les modifications ont été annulées.`r`n`r`nVous pouvez maintenant supprimer sans risque tout le contenu de ce dossier.`r`nPour le conserver, relancez start.cmd à tout moment pour redéployer le service."
        btnDelete  = 'Tout supprimer'
        btnClose   = 'Fermer'
    }

    'ru' = @{
        startTitle = 'Sleepify - Рискованная платформа'
        startBody  = "Эта программа на много часов принудительно снижает нагрузку на CPU.`r`n`r`nНа мобильных AMD Ryzen 5000 / 6000 / 7000 серии H (H / HS / HX) низкая нагрузка и низкое напряжение - это именно тот режим, в котором деградировавший SoC (особенно контроллер памяти) становится нестабильным или падает.`r`n`r`nОбнаруженный процессор:`r`n{CPU}`r`n`r`nВсё равно продолжить?"
        finalTitle = 'Sleepify'
        finalBody  = "Все изменения отменены.`r`n`r`nТеперь можно безопасно удалить всё содержимое этой папки.`r`nЕсли хотите оставить - запустите start.cmd, чтобы снова развернуть сервис."
        btnDelete  = 'Удалить всё'
        btnClose   = 'Закрыть'
    }

    'es' = @{
        startTitle = 'Sleepify - Plataforma de riesgo'
        startBody  = "Esta herramienta fuerza una carga de CPU muy baja durante horas.`r`n`r`nEn los AMD Ryzen 5000 / 6000 / 7000 móviles serie H (H / HS / HX), la carga y el voltaje bajos son justo el régimen en el que un SoC degradado - especialmente el controlador de memoria - se vuelve inestable o falla.`r`n`r`nCPU detectada:`r`n{CPU}`r`n`r`n¿Continuar de todos modos?"
        finalTitle = 'Sleepify'
        finalBody  = "Se han revertido todos los cambios.`r`n`r`nAhora puede eliminar sin riesgo todo el contenido de esta carpeta.`r`nSi prefiere conservarla, vuelva a ejecutar start.cmd para desplegar el servicio de nuevo."
        btnDelete  = 'Eliminar todo'
        btnClose   = 'Cerrar'
    }

    'ar' = @{
        startTitle = 'Sleepify - منصة عالية الخطورة'
        startBody  = "تفرض هذه الأداة حمل معالج منخفضاً جداً لساعات.`r`n`r`nفي معالجات AMD Ryzen 5000 / 6000 / 7000 المحمولة من الفئة H (H / HS / HX)، يُعد الحمل والجهد المنخفضان تحديداً النظام الذي يصبح فيه SoC المتدهور - وخاصة متحكم الذاكرة - غير مستقر أو ينهار.`r`n`r`nالمعالج المكتشف:`r`n{CPU}`r`n`r`nهل تريد المتابعة على أي حال؟"
        finalTitle = 'Sleepify'
        finalBody  = "تم التراجع عن جميع التغييرات.`r`n`r`nيمكنك الآن حذف كل محتويات هذا المجلد بأمان.`r`nوإذا رغبت في الاحتفاظ به، شغّل start.cmd لإعادة نشر الخدمة."
        btnDelete  = 'حذف الكل'
        btnClose   = 'إغلاق'
    }
}

function Get-SleepifyString([string]$key) {
    $lang = Get-SleepifyLang
    if (-not $script:SleepifyStrings.ContainsKey($lang)) { $lang = 'en' }
    $v = $script:SleepifyStrings[$lang][$key]
    if (-not $v) { $v = $script:SleepifyStrings['en'][$key] }
    return $v
}
