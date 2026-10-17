namespace DurusMerkezi
{
    /// <summary>
    /// Uygulamanın teması: nötr gri yüzeyler (tasarımın koyu, renksiz zemini), mavi vurgu, tasarımdaki ek ikonlar ve
    /// uygulamaya özgü bileşenler. Ekranlar renkleri doğrudan değil bu bileşenlerle verir; böylece açık ve koyu tema tek
    /// yerden değişir.
    /// </summary>
    internal static class Theme
    {
        public const string Json = @"{
  ""fontFamily"": ""Segoe UI"",
  ""colors"": {
    ""surface"": { ""50"": ""#fafafa"", ""100"": ""#f4f4f5"", ""200"": ""#e4e4e7"", ""300"": ""#d4d4d8"", ""400"": ""#a1a1aa"", ""500"": ""#71717a"",
                   ""600"": ""#52525b"", ""700"": ""#38383b"", ""800"": ""#28282a"", ""900"": ""#1e1e20"", ""950"": ""#151517"" }
  },
  ""icons"": {
    ""sidebar"": ""<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><rect width='18' height='18' x='3' y='3' rx='2'/><path d='M9 3v18'/></svg>"",
    ""table"": ""<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><rect width='18' height='18' x='3' y='3' rx='2'/><path d='M3 9h18'/><path d='M3 15h18'/><path d='M9 3v18'/></svg>"",
    ""chart"": ""<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M3 3v18h18'/><path d='m19 9-5 5-4-4-3 3'/></svg>"",
    ""compare"": ""<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='m21 16-4 4-4-4'/><path d='M17 20V4'/><path d='m3 8 4-4 4 4'/><path d='M7 4v16'/></svg>"",
    ""columns"": ""<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><rect width='18' height='18' x='3' y='3' rx='2'/><path d='M15 3v18'/></svg>""
  },
  ""components"": {
    ""page"": ""font-sans bg-surface-100 text-surface-900 dark:bg-surface-900 dark:text-surface-100"",
    ""card"": ""bg-white border border-surface-200 rounded-xl p-4 dark:bg-surface-800 dark:border-surface-800"",
    ""tile"": ""bg-surface-50 border border-surface-200 rounded-lg px-3 py-2 dark:bg-[#303033] dark:border-[#303033]"",
    ""well"": ""bg-white border border-surface-300 rounded-md dark:bg-surface-950 dark:border-surface-700"",
    ""sidebar"": ""bg-surface-50 border-r border-surface-200 dark:bg-surface-950 dark:border-surface-800"",
    ""toolbar"": ""bg-white border-b border-surface-200 dark:bg-surface-900 dark:border-surface-800"",
    ""toolbar-top"": ""bg-white dark:bg-surface-900"",
    ""h1"": ""text-2xl font-bold text-surface-900 dark:text-white"",
    ""h2"": ""text-sm font-semibold text-surface-900 dark:text-surface-50"",
    ""strong"": ""text-sm font-semibold text-surface-900 dark:text-surface-50"",
    ""body"": ""text-sm text-surface-700 dark:text-surface-200"",
    ""muted"": ""text-sm text-surface-500 dark:text-surface-400"",
    ""small"": ""text-xs text-surface-500 dark:text-surface-400"",
    ""section"": ""text-xs font-semibold text-surface-500 dark:text-surface-500"",
    ""mono"": ""font-mono text-xs text-surface-500 dark:text-surface-400"",
    ""big"": ""text-2xl font-bold text-surface-900 dark:text-white"",
    ""kpi"": ""text-[26px] font-semibold text-surface-900 dark:text-white"",
    ""nav"": ""px-2 rounded-md text-sm text-surface-700 cursor-pointer hover:bg-surface-200 dark:text-surface-200 dark:hover:bg-surface-800"",
    ""nav-on"": ""px-2 rounded-md text-sm cursor-pointer bg-surface-200 text-surface-900 dark:bg-surface-800 dark:text-white"",
    ""nav-icon"": ""w-4 h-4 text-primary-600 dark:text-primary-500"",
    ""btn-sm"": ""btn px-3 py-1 text-xs font-medium bg-surface-200 text-surface-800 hover:bg-surface-300 dark:bg-[#434347] dark:text-surface-100 dark:hover:bg-[#4d4d52]"",
    ""btn-sm-primary"": ""btn px-3 py-1 text-xs font-semibold bg-primary-600 text-white hover:bg-primary-700 dark:bg-primary-600 dark:hover:bg-primary-700"",
    ""btn-plain"": ""btn px-2 py-1 text-sm font-normal bg-transparent text-surface-700 hover:bg-surface-200 dark:bg-transparent dark:text-surface-200 dark:hover:bg-surface-800"",
    ""btn-icon"": ""btn px-1.5 py-1 bg-primary-100 text-primary-700 hover:bg-primary-200 dark:bg-primary-950 dark:text-primary-400 dark:hover:bg-primary-900"",
    ""kbd"": ""font-mono text-xs text-center rounded px-1 bg-surface-200 text-surface-600 dark:bg-surface-700 dark:text-surface-300"",
    ""chip"": ""text-xs font-semibold rounded-full px-2 text-center"",
    ""chip-gray"": ""chip bg-surface-200 text-surface-700 dark:bg-surface-700 dark:text-surface-200"",
    ""chip-blue"": ""chip bg-primary-100 text-primary-800 dark:bg-primary-950 dark:text-primary-400"",
    ""chip-green"": ""chip bg-success-100 text-success-800 dark:bg-success-950 dark:text-success-400"",
    ""chip-amber"": ""chip bg-warning-100 text-warning-800 dark:bg-warning-950 dark:text-warning-400"",
    ""chip-red"": ""chip bg-danger-100 text-danger-800 dark:bg-danger-950 dark:text-danger-400"",
    ""chip-violet"": ""chip bg-violet-100 text-violet-800 dark:bg-violet-950 dark:text-violet-300"",
    ""alert-root"": ""p-3 rounded-lg border bg-danger-50 border-danger-200 dark:bg-danger-950 dark:border-danger-900"",
    ""hero"": ""rounded-xl border p-5 bg-danger-50 border-surface-200 dark:bg-[#312526] dark:border-[#3a2a2b]"",
    ""bubble"": ""bg-surface-100 rounded-lg px-3 py-2 dark:bg-surface-700"",
    ""avatar"": ""text-xs font-bold rounded-full text-center bg-surface-200 text-surface-700 dark:bg-surface-700 dark:text-surface-100"",
    ""avatar-ring"": ""text-xs font-bold rounded-full text-center bg-transparent border border-surface-300 text-surface-700 dark:bg-transparent dark:border-surface-600 dark:text-surface-100"",
    ""search"": ""input flex items-center gap-2 px-2 h-8 rounded-md bg-white dark:bg-surface-800 dark:border-surface-700"",
    ""table-plain"": ""table table-sm border-0 bg-white header:bg-white header:font-normal header:text-surface-500 dark:bg-surface-800 dark:header:bg-surface-800 dark:header:text-surface-400 dark:divide-surface-700""
  }
}";
    }
}
