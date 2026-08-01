(function (global) {
  const CONDITION_META = {
    A: { group: 1, label: "個人利用", full: "個人による利用" },
    B: { group: 1, label: "法人利用", full: "法人による利用" },
    C: {
      group: 2,
      label: "ソーシャルプラットフォームへのアップロード",
      full: "ソーシャルコミュニケーションプラットフォームへのアップロード",
    },
    D: {
      group: 2,
      label: "オンラインゲームプラットフォームへのアップロード",
      full: "オンラインゲームプラットフォームへのアップロード",
    },
    E: {
      group: 2,
      label: "オンラインサービス内での第三者への利用の許諾",
      full: "オンラインサービス内での第三者への利用の許諾",
    },
    F: { group: 3, label: "性的表現", full: "性的表現への利用" },
    G: { group: 3, label: "暴力的表現", full: "暴力的表現への利用" },
    H: {
      group: 3,
      label: "政治活動・宗教活動",
      full: "政治活動への利用および、宗教活動への利用",
    },
    I: { group: 4, label: "調整", full: "調整" },
    J: { group: 4, label: "改変", full: "改変" },
    K: {
      group: 4,
      label: "他データ改変目的での利用",
      full: "他のデータを改変するための利用",
    },
    L: {
      group: 4,
      label: "調整・改変の外部委託",
      full: "調整・改変の外部委託",
    },
    M: {
      group: 5,
      label: "未改変状態での再配布",
      full: "未改変状態での再配布",
    },
    N: {
      group: 5,
      label: "改変したデータの配布",
      full: "改変したデータの配布",
    },
    O: {
      group: 6,
      label: "映像作品・配信・放送",
      full: "映像作品・配信・放送への利用",
    },
    P: {
      group: 6,
      label: "出版物・電子出版物",
      full: "出版物・電子出版物への利用",
    },
    Q: {
      group: 6,
      label: "有体物（グッズ）",
      full: "有体物（グッズ）への利用",
    },
    R: {
      group: 6,
      label: "ソフトウェアへの組み込み",
      full: "製品開発等のためのソフトウェアへの組み込み",
    },
    S: {
      group: 7,
      label: "メッシュ・ウェイト転用した衣装データの作成",
      full: "メッシュやウェイトを転用した衣装データの作成",
    },
    T: {
      group: 7,
      label: "規格準拠の新たなデータの作成",
      full: "メッシュやウェイトを転用しない規格準拠の新たなデータ作成",
    },
    U: {
      group: 7,
      label: "データをモチーフにした二次的著作物",
      full: "データをモチーフにした二次的著作物（いわゆる二次創作）",
    },
    V: { group: 8, label: "クレジット表記", full: "クレジット表記" },
    W: { group: 8, label: "権利義務の譲渡等", full: "権利義務の譲渡等" },
    X: { group: 9, label: "特記事項", full: "特記事項" },
  };

  const GROUPS = [
    { id: 1, title: "利用主体" },
    { id: 2, title: "オンラインサービスへのアップロード" },
    { id: 3, title: "センシティブな表現" },
    { id: 4, title: "加工" },
    { id: 5, title: "再配布・配布" },
    { id: 6, title: "メディア・プロダクトへの使用" },
    { id: 7, title: "二次創作" },
    { id: 8, title: "その他" },
    { id: 9, title: "特記事項" },
  ];

  const META_KEYS = [
    { key: "target", patterns: [/^【許諾対象データ】\s*(.*)$/] },
    { key: "rightsHolder", patterns: [/^【権利者】\s*(.*)$/] },
    { key: "contact", patterns: [/^【問い合わせ先】\s*(.*)$/] },
    { key: "credit", patterns: [/^【クレジット表記】\s*(.*)$/] },
    { key: "hashtags", patterns: [/^【推奨ハッシュタグ】\s*(.*)$/] },
    { key: "version", patterns: [/^【利用規約バージョン】\s*(.*)$/] },
  ];

  function classifyStatus(value) {
    const text = (value || "").trim();
    if (!text || text === "—" || text === "-" || text === "ー") {
      return { kind: "empty", label: "—" };
    }
    if (/^許可/.test(text) && !/不許可|許可しません|許可しない/.test(text)) {
      return { kind: "allow", label: text };
    }
    if (/不許可|許可しません|許可しない|禁止/.test(text)) {
      return { kind: "deny", label: text };
    }
    if (/必要|要（|要$|^要/.test(text)) {
      return { kind: "required", label: text };
    }
    if (/不要/.test(text)) {
      return { kind: "optional", label: text };
    }
    if (/問い合わせ|要確認|個別/.test(text)) {
      return { kind: "ask", label: text };
    }
    return { kind: "other", label: text };
  }

  function extractBlock(lines, startPattern, endPatterns) {
    const start = lines.findIndex((line) => startPattern.test(line));
    if (start < 0) return "";
    const collected = [];
    for (let i = start + 1; i < lines.length; i += 1) {
      const line = lines[i];
      if (endPatterns.some((re) => re.test(line))) break;
      collected.push(line);
    }
    return collected.join("\n").trim();
  }

  function parseLicenseText(rawText, options = {}) {
    const text = String(rawText || "").replace(/^\uFEFF/, "");
    const lines = text.split(/\r?\n/);
    const meta = {
      target: "",
      rightsHolder: "",
      contact: "",
      credit: "",
      hashtags: "",
      term: "",
      version: "",
      specialNote: "",
    };

    for (const line of lines) {
      for (const def of META_KEYS) {
        for (const pattern of def.patterns) {
          const match = line.match(pattern);
          if (match) {
            meta[def.key] = (match[1] || "").trim();
          }
        }
      }
    }

    meta.term = extractBlock(
      lines,
      /^【許諾期間】/,
      [/^【/, /^─{3,}/, /^═{3,}/, /^【個別条件】/, /^【X /]
    );

    meta.specialNote = extractBlock(
      lines,
      /^【X 特記事項】/,
      [/^─{3,}/, /^═{3,}/, /^本記載のほか/, /^【/]
    );

    const conditions = {};
    for (const line of lines) {
      const match = line.match(
        /^\s*([A-WX])\s+(.+?)\s*[:：]\s*(.+?)\s*$/
      );
      if (!match) continue;
      const code = match[1];
      const shortLabel = match[2].trim();
      const value = match[3].trim();
      const known = CONDITION_META[code] || {
        group: 0,
        label: shortLabel,
        full: shortLabel,
      };
      conditions[code] = {
        code,
        label: known.label || shortLabel,
        full: known.full || shortLabel,
        group: known.group || 0,
        value,
        status: classifyStatus(value),
      };
    }

    // X may be only in special note without A-W style line
    if (!conditions.X && meta.specialNote) {
      conditions.X = {
        code: "X",
        label: CONDITION_META.X.label,
        full: CONDITION_META.X.full,
        group: 9,
        value: meta.specialNote,
        status: { kind: "other", label: meta.specialNote },
      };
    }

    const titleGuess =
      options.title ||
      meta.target ||
      options.product ||
      "利用規約";

    return {
      title: titleGuess,
      product: options.product || "",
      version: meta.version || "1.10",
      meta,
      conditions,
      groups: GROUPS,
      conditionMeta: CONDITION_META,
      rawText: text,
    };
  }

  global.LicenseParser = {
    parseLicenseText,
    CONDITION_META,
    GROUPS,
    classifyStatus,
  };
})(window);
