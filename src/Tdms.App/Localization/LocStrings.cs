namespace Tdms.App.Localization;

/// <summary>
/// The per-language string tables. A key missing from a table falls back to
/// <see cref="AppLanguage.En"/>, and a key missing everywhere returns itself.
/// </summary>
public static class LocStrings
{
    /// <summary>Language names, each written in its own language.</summary>
    public static readonly IReadOnlyDictionary<AppLanguage, string> LanguageNames =
        new Dictionary<AppLanguage, string>
        {
            [AppLanguage.En] = "English",
            [AppLanguage.Ko] = "한국어",
            [AppLanguage.Ja] = "日本語",
            [AppLanguage.De] = "Deutsch",
            [AppLanguage.Zh] = "简体中文",
        };

    private static readonly Dictionary<string, string> En = new()
    {
        ["FormatXlsx"] = "Excel workbook (.xlsx)",
        ["XlsxFiles"] = "Excel workbooks",

        // Toolbar
        ["Open"] = "Open",
        ["OpenTip"] = "Open a .tdms file (the .tdms_index sidecar is used when present)",
        ["Reload"] = "Reload",
        ["Export"] = "Export",
        ["Language"] = "Language",

        // Stat tiles
        ["StatFileSize"] = "FILE SIZE",
        ["StatGroups"] = "GROUPS",
        ["StatChannels"] = "CHANNELS",
        ["StatSamples"] = "SAMPLES",

        // Sections
        ["Channels"] = "Channels",
        ["Properties"] = "Properties",
        ["NoFileTitle"] = "No file open",
        ["NoFileHint"] = "Open a .tdms file to see its groups, channels and properties.",
        ["NoSelection"] = "Select a group or channel to see its properties.",
        ["FileNode"] = "File",
        ["LocalOnly"] = "Runs entirely on this machine — nothing is uploaded.",

        // Property table
        ["ColProperty"] = "Property",
        ["ColType"] = "Type",
        ["ColValue"] = "Value",

        // Status
        ["StatusNoFile"] = "No file",
        ["StatusLoading"] = "Reading…",
        ["StatusReady"] = "Ready",
        ["StatusIndex"] = "read from index sidecar",
        ["StatusDataFile"] = "read from data file",
        ["StatusTruncated"] = "incomplete tail — the file was cut mid-write",
        ["LoadFailed"] = "Could not read the file",

        // Units
        ["UnitSamples"] = "samples",
        ["UnitChannels"] = "channels",
        ["UnitSegments"] = "segments",

        // Export dialog
        ["ExportTitle"] = "Export channels",
        ["SelectChannels"] = "Channels to export",
        ["SelectAll"] = "Select all",
        ["SelectNone"] = "Select none",
        ["Format"] = "Format",
        ["FormatCsv"] = "CSV",
        ["FormatCsvProperties"] = "CSV with property header",
        ["Delimiter"] = "Delimiter",
        ["DelimiterComma"] = "Comma  ,",
        ["DelimiterSemicolon"] = "Semicolon  ;",
        ["DelimiterTab"] = "Tab",
        ["IncludeTime"] = "Time column from waveform properties",
        ["ExportButton"] = "Export",
        ["Cancel"] = "Cancel",
        ["Close"] = "Close",
        ["Exporting"] = "Exporting…",
        ["ExportDone"] = "Export finished",
        ["ExportFailed"] = "Export failed",
        ["ExportCancelled"] = "Export cancelled",
        ["NoChannelsSelected"] = "Select at least one channel.",

        // File pickers
        ["OpenDialogTitle"] = "Open TDMS file",
        ["SaveDialogTitle"] = "Save CSV",
        ["TdmsFiles"] = "TDMS measurement files",
        ["CsvFiles"] = "CSV files",
    };

    private static readonly Dictionary<string, string> Ko = new()
    {
        ["FormatXlsx"] = "Excel 통합 문서 (.xlsx)",
        ["XlsxFiles"] = "Excel 통합 문서",

        ["Open"] = "열기",
        ["OpenTip"] = ".tdms 파일 열기 (.tdms_index 사이드카가 있으면 함께 사용)",
        ["Reload"] = "다시 읽기",
        ["Export"] = "내보내기",
        ["Language"] = "언어",

        ["StatFileSize"] = "파일 크기",
        ["StatGroups"] = "그룹",
        ["StatChannels"] = "채널",
        ["StatSamples"] = "샘플",

        ["Channels"] = "채널",
        ["Properties"] = "속성",
        ["NoFileTitle"] = "열린 파일 없음",
        ["NoFileHint"] = ".tdms 파일을 열면 그룹·채널·속성을 볼 수 있습니다.",
        ["NoSelection"] = "그룹이나 채널을 선택하면 속성이 표시됩니다.",
        ["FileNode"] = "파일",
        ["LocalOnly"] = "모든 처리는 이 컴퓨터에서만 이루어집니다 — 업로드 없음.",

        ["ColProperty"] = "속성",
        ["ColType"] = "타입",
        ["ColValue"] = "값",

        ["StatusNoFile"] = "파일 없음",
        ["StatusLoading"] = "읽는 중…",
        ["StatusReady"] = "준비됨",
        ["StatusIndex"] = "인덱스 사이드카에서 읽음",
        ["StatusDataFile"] = "데이터 파일에서 읽음",
        ["StatusTruncated"] = "끝부분 손상 — 기록 도중 중단된 파일",
        ["LoadFailed"] = "파일을 읽지 못했습니다",

        ["UnitSamples"] = "샘플",
        ["UnitChannels"] = "채널",
        ["UnitSegments"] = "세그먼트",

        ["ExportTitle"] = "채널 내보내기",
        ["SelectChannels"] = "내보낼 채널",
        ["SelectAll"] = "전체 선택",
        ["SelectNone"] = "선택 해제",
        ["Format"] = "형식",
        ["FormatCsv"] = "CSV",
        ["FormatCsvProperties"] = "CSV + 속성 헤더",
        ["Delimiter"] = "구분자",
        ["DelimiterComma"] = "쉼표  ,",
        ["DelimiterSemicolon"] = "세미콜론  ;",
        ["DelimiterTab"] = "탭",
        ["IncludeTime"] = "웨이브폼 속성으로 시간 열 생성",
        ["ExportButton"] = "내보내기",
        ["Cancel"] = "취소",
        ["Close"] = "닫기",
        ["Exporting"] = "내보내는 중…",
        ["ExportDone"] = "내보내기 완료",
        ["ExportFailed"] = "내보내기 실패",
        ["ExportCancelled"] = "내보내기 취소됨",
        ["NoChannelsSelected"] = "채널을 하나 이상 선택하세요.",

        ["OpenDialogTitle"] = "TDMS 파일 열기",
        ["SaveDialogTitle"] = "CSV 저장",
        ["TdmsFiles"] = "TDMS 측정 파일",
        ["CsvFiles"] = "CSV 파일",
    };

    private static readonly Dictionary<string, string> Ja = new()
    {
        ["FormatXlsx"] = "Excel ブック (.xlsx)",
        ["XlsxFiles"] = "Excel ブック",

        ["Open"] = "開く",
        ["OpenTip"] = ".tdms ファイルを開く（.tdms_index があれば併用）",
        ["Reload"] = "再読み込み",
        ["Export"] = "エクスポート",
        ["Language"] = "言語",

        ["StatFileSize"] = "ファイルサイズ",
        ["StatGroups"] = "グループ",
        ["StatChannels"] = "チャンネル",
        ["StatSamples"] = "サンプル",

        ["Channels"] = "チャンネル",
        ["Properties"] = "プロパティ",
        ["NoFileTitle"] = "ファイル未選択",
        ["NoFileHint"] = ".tdms ファイルを開くと、グループ・チャンネル・プロパティが表示されます。",
        ["NoSelection"] = "グループまたはチャンネルを選ぶとプロパティが表示されます。",
        ["FileNode"] = "ファイル",
        ["LocalOnly"] = "すべてこの PC 内で処理されます — アップロードはありません。",

        ["ColProperty"] = "プロパティ",
        ["ColType"] = "型",
        ["ColValue"] = "値",

        ["StatusNoFile"] = "ファイルなし",
        ["StatusLoading"] = "読み込み中…",
        ["StatusReady"] = "準備完了",
        ["StatusIndex"] = "インデックスファイルから読み込み",
        ["StatusDataFile"] = "データファイルから読み込み",
        ["StatusTruncated"] = "末尾が不完全 — 書き込み中に中断されたファイル",
        ["LoadFailed"] = "ファイルを読み込めませんでした",

        ["UnitSamples"] = "サンプル",
        ["UnitChannels"] = "チャンネル",
        ["UnitSegments"] = "セグメント",

        ["ExportTitle"] = "チャンネルのエクスポート",
        ["SelectChannels"] = "エクスポートするチャンネル",
        ["SelectAll"] = "すべて選択",
        ["SelectNone"] = "選択解除",
        ["Format"] = "形式",
        ["FormatCsv"] = "CSV",
        ["FormatCsvProperties"] = "CSV + プロパティヘッダー",
        ["Delimiter"] = "区切り文字",
        ["DelimiterComma"] = "カンマ  ,",
        ["DelimiterSemicolon"] = "セミコロン  ;",
        ["DelimiterTab"] = "タブ",
        ["IncludeTime"] = "波形プロパティから時間列を生成",
        ["ExportButton"] = "エクスポート",
        ["Cancel"] = "キャンセル",
        ["Close"] = "閉じる",
        ["Exporting"] = "エクスポート中…",
        ["ExportDone"] = "エクスポート完了",
        ["ExportFailed"] = "エクスポート失敗",
        ["ExportCancelled"] = "エクスポートを中止しました",
        ["NoChannelsSelected"] = "チャンネルを 1 つ以上選択してください。",

        ["OpenDialogTitle"] = "TDMS ファイルを開く",
        ["SaveDialogTitle"] = "CSV を保存",
        ["TdmsFiles"] = "TDMS 測定ファイル",
        ["CsvFiles"] = "CSV ファイル",
    };

    private static readonly Dictionary<string, string> De = new()
    {
        ["FormatXlsx"] = "Excel-Arbeitsmappe (.xlsx)",
        ["XlsxFiles"] = "Excel-Arbeitsmappen",

        ["Open"] = "Öffnen",
        ["OpenTip"] = ".tdms-Datei öffnen (die .tdms_index-Datei wird genutzt, wenn vorhanden)",
        ["Reload"] = "Neu laden",
        ["Export"] = "Exportieren",
        ["Language"] = "Sprache",

        ["StatFileSize"] = "DATEIGRÖSSE",
        ["StatGroups"] = "GRUPPEN",
        ["StatChannels"] = "KANÄLE",
        ["StatSamples"] = "MESSWERTE",

        ["Channels"] = "Kanäle",
        ["Properties"] = "Eigenschaften",
        ["NoFileTitle"] = "Keine Datei geöffnet",
        ["NoFileHint"] = "Öffnen Sie eine .tdms-Datei, um Gruppen, Kanäle und Eigenschaften zu sehen.",
        ["NoSelection"] = "Wählen Sie eine Gruppe oder einen Kanal, um die Eigenschaften zu sehen.",
        ["FileNode"] = "Datei",
        ["LocalOnly"] = "Läuft vollständig auf diesem Rechner — nichts wird hochgeladen.",

        ["ColProperty"] = "Eigenschaft",
        ["ColType"] = "Typ",
        ["ColValue"] = "Wert",

        ["StatusNoFile"] = "Keine Datei",
        ["StatusLoading"] = "Wird gelesen…",
        ["StatusReady"] = "Bereit",
        ["StatusIndex"] = "aus der Indexdatei gelesen",
        ["StatusDataFile"] = "aus der Datendatei gelesen",
        ["StatusTruncated"] = "unvollständiges Ende — die Datei wurde beim Schreiben abgebrochen",
        ["LoadFailed"] = "Die Datei konnte nicht gelesen werden",

        ["UnitSamples"] = "Messwerte",
        ["UnitChannels"] = "Kanäle",
        ["UnitSegments"] = "Segmente",

        ["ExportTitle"] = "Kanäle exportieren",
        ["SelectChannels"] = "Zu exportierende Kanäle",
        ["SelectAll"] = "Alle auswählen",
        ["SelectNone"] = "Auswahl aufheben",
        ["Format"] = "Format",
        ["FormatCsv"] = "CSV",
        ["FormatCsvProperties"] = "CSV mit Eigenschaftskopf",
        ["Delimiter"] = "Trennzeichen",
        ["DelimiterComma"] = "Komma  ,",
        ["DelimiterSemicolon"] = "Semikolon  ;",
        ["DelimiterTab"] = "Tabulator",
        ["IncludeTime"] = "Zeitspalte aus den Waveform-Eigenschaften",
        ["ExportButton"] = "Exportieren",
        ["Cancel"] = "Abbrechen",
        ["Close"] = "Schließen",
        ["Exporting"] = "Export läuft…",
        ["ExportDone"] = "Export abgeschlossen",
        ["ExportFailed"] = "Export fehlgeschlagen",
        ["ExportCancelled"] = "Export abgebrochen",
        ["NoChannelsSelected"] = "Wählen Sie mindestens einen Kanal.",

        ["OpenDialogTitle"] = "TDMS-Datei öffnen",
        ["SaveDialogTitle"] = "CSV speichern",
        ["TdmsFiles"] = "TDMS-Messdateien",
        ["CsvFiles"] = "CSV-Dateien",
    };

    private static readonly Dictionary<string, string> Zh = new()
    {
        ["FormatXlsx"] = "Excel 工作簿 (.xlsx)",
        ["XlsxFiles"] = "Excel 工作簿",

        ["Open"] = "打开",
        ["OpenTip"] = "打开 .tdms 文件（若存在 .tdms_index 索引文件则一并使用）",
        ["Reload"] = "重新读取",
        ["Export"] = "导出",
        ["Language"] = "语言",

        ["StatFileSize"] = "文件大小",
        ["StatGroups"] = "分组",
        ["StatChannels"] = "通道",
        ["StatSamples"] = "采样点",

        ["Channels"] = "通道",
        ["Properties"] = "属性",
        ["NoFileTitle"] = "尚未打开文件",
        ["NoFileHint"] = "打开 .tdms 文件即可查看其分组、通道与属性。",
        ["NoSelection"] = "选择分组或通道以查看其属性。",
        ["FileNode"] = "文件",
        ["LocalOnly"] = "全部处理都在本机完成 — 不会上传任何数据。",

        ["ColProperty"] = "属性",
        ["ColType"] = "类型",
        ["ColValue"] = "值",

        ["StatusNoFile"] = "无文件",
        ["StatusLoading"] = "正在读取…",
        ["StatusReady"] = "就绪",
        ["StatusIndex"] = "读自索引文件",
        ["StatusDataFile"] = "读自数据文件",
        ["StatusTruncated"] = "尾部不完整 — 文件在写入过程中被中断",
        ["LoadFailed"] = "无法读取该文件",

        ["UnitSamples"] = "采样点",
        ["UnitChannels"] = "通道",
        ["UnitSegments"] = "段",

        ["ExportTitle"] = "导出通道",
        ["SelectChannels"] = "要导出的通道",
        ["SelectAll"] = "全选",
        ["SelectNone"] = "全不选",
        ["Format"] = "格式",
        ["FormatCsv"] = "CSV",
        ["FormatCsvProperties"] = "CSV + 属性头",
        ["Delimiter"] = "分隔符",
        ["DelimiterComma"] = "逗号  ,",
        ["DelimiterSemicolon"] = "分号  ;",
        ["DelimiterTab"] = "制表符",
        ["IncludeTime"] = "根据波形属性生成时间列",
        ["ExportButton"] = "导出",
        ["Cancel"] = "取消",
        ["Close"] = "关闭",
        ["Exporting"] = "正在导出…",
        ["ExportDone"] = "导出完成",
        ["ExportFailed"] = "导出失败",
        ["ExportCancelled"] = "导出已取消",
        ["NoChannelsSelected"] = "请至少选择一个通道。",

        ["OpenDialogTitle"] = "打开 TDMS 文件",
        ["SaveDialogTitle"] = "保存 CSV",
        ["TdmsFiles"] = "TDMS 测量文件",
        ["CsvFiles"] = "CSV 文件",
    };

    private static readonly IReadOnlyDictionary<AppLanguage, Dictionary<string, string>> Tables =
        new Dictionary<AppLanguage, Dictionary<string, string>>
        {
            [AppLanguage.En] = En,
            [AppLanguage.Ko] = Ko,
            [AppLanguage.Ja] = Ja,
            [AppLanguage.De] = De,
            [AppLanguage.Zh] = Zh,
        };

    /// <summary>Every key defined in the English table (the tests check for gaps).</summary>
    public static IEnumerable<string> AllKeys => En.Keys;

    /// <summary>Looks a key up, falling back to English and then to the key itself.</summary>
    /// <param name="language">Display language.</param>
    /// <param name="key">String key.</param>
    /// <returns>The translated string.</returns>
    public static string Get(AppLanguage language, string key)
    {
        if (Tables.TryGetValue(language, out var table) && table.TryGetValue(key, out var text))
        {
            return text;
        }

        return En.TryGetValue(key, out var fallback) ? fallback : key;
    }

    /// <summary>Keys actually defined for a language, ignoring the English fallback.</summary>
    /// <param name="language">Display language.</param>
    /// <returns>The defined keys.</returns>
    public static IEnumerable<string> KeysDefinedIn(AppLanguage language) =>
        Tables.TryGetValue(language, out var table) ? table.Keys : Array.Empty<string>();
}
