window.LicenseViewerConfig = {
  /** マニフェスト（自動生成） */
  manifestUrl: "./licenses.json",

  /**
   * ライセンス本文の探索順。
   * GitHub Pages で LicenseViewer のみ公開する場合は local を利用。
   * リポジトリ全体を公開する場合は source でも取得できます。
   */
  fetchCandidates: (entry) => [
    entry.localPath,
    entry.sourcePath,
    `../SamirinBoothInformation/${entry.relativePath}`,
  ],

  /**
   * GitHub raw 取得用（任意）。
   * 例: owner: "yourname", repo: "SamirinBoothManager", branch: "main",
   *     informationPath: "SamirinBoothManager/SamirinBoothInformation"
   */
  github: {
    owner: "",
    repo: "",
    branch: "main",
    informationPath: "SamirinBoothManager/SamirinBoothInformation",
  },

  vn3OfficialUrl: "https://www.vn3.org/",
  vn3TermsUrl: "https://www.vn3.org/terms",
  documentReferenceUrl:
    "https://docs.google.com/document/d/1r5exLnCwh1Bny-cH1ZPrjIX8wrnfDfZspCQq1b92ZYo/edit?tab=t.0",
};
