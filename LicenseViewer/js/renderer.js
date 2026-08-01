(function (global) {
  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  function linkify(text) {
    const escaped = escapeHtml(text);
    return escaped.replace(
      /(https?:\/\/[^\s<>"']+)/g,
      '<a href="$1" target="_blank" rel="noopener noreferrer">$1</a>'
    );
  }

  function statusBadge(status) {
    const kind = status?.kind || "other";
    const label = status?.label || "—";
    return `<span class="status status-${kind}" role="status">${escapeHtml(label)}</span>`;
  }

  function metaValue(value) {
    const text = (value || "").trim();
    if (!text || text === "—" || text === "-" || text === "ー") {
      return '<span class="muted">—</span>';
    }
    return linkify(text);
  }

  function renderSummaryTable(license) {
    return license.groups
      .map((group) => {
        const rows = Object.values(license.conditions)
          .filter((c) => c.group === group.id)
          .sort((a, b) => a.code.localeCompare(b.code));

        if (!rows.length) return "";

        const body = rows
          .map((row) => {
            const detail =
              group.id === 9
                ? `<div class="note-block">${linkify(row.value)}</div>`
                : statusBadge(row.status);
            return `
              <tr>
                <th scope="row"><span class="code">${escapeHtml(row.code)}</span></th>
                <td class="cond-label">${escapeHtml(row.full || row.label)}</td>
                <td class="cond-value">${detail}</td>
              </tr>`;
          })
          .join("");

        return `
          <section class="summary-group">
            <h3 class="md-title-md"><span class="group-num">${group.id}.</span> ${escapeHtml(group.title)}</h3>
            <table class="summary-table">
              <tbody>${body}</tbody>
            </table>
          </section>`;
      })
      .join("");
  }

  function renderMetaPanel(license) {
    const { meta } = license;
    const items = [
      ["許諾対象データ", meta.target],
      ["権利者", meta.rightsHolder],
      ["問い合わせ先", meta.contact],
      ["クレジット表記", meta.credit],
      ["推奨ハッシュタグ", meta.hashtags],
      ["利用規約バージョン", meta.version || license.version],
    ];

    const rows = items
      .map(
        ([label, value]) => `
        <div class="meta-item">
          <dt>${escapeHtml(label)}</dt>
          <dd>${metaValue(value)}</dd>
        </div>`
      )
      .join("");

    const term = meta.term
      ? `<div class="meta-item meta-item-wide">
           <dt>許諾期間および許諾の変更等</dt>
           <dd>${linkify(meta.term)}</dd>
         </div>`
      : "";

    return `<dl class="meta-grid">${rows}${term}</dl>`;
  }

  function renderRaw(license) {
    return `<pre class="raw-license">${escapeHtml(license.rawText)}</pre>`;
  }

  function renderDocument(license, config) {
    const productName = escapeHtml(license.title || license.product || "本データ");
    return `
      <article class="license-doc">
        <header class="doc-header">
          <p class="eyebrow">VN3 License based terms</p>
          <h1>${productName}</h1>
          <p class="doc-sub">利用規約による許諾範囲の簡易一覧</p>
          <p class="doc-note">必ず利用規約本文を併せてご確認ください。本ページは
            <a href="${escapeHtml(config.documentReferenceUrl)}" target="_blank" rel="noopener noreferrer">VN3ライセンス公開書式</a>
            を参考にした表示です。
          </p>
        </header>

        <section class="md-section">
          <h2 class="md-section__title">許諾範囲の簡易一覧</h2>
          ${renderSummaryTable(license)}
        </section>

        <section class="md-section">
          <h2 class="md-section__title">権利者情報・表記</h2>
          ${renderMetaPanel(license)}
        </section>

        <section class="md-section">
          <h2 class="md-section__title">利用規約本文（収録テキスト）</h2>
          <p class="doc-note">
            本データは VN3ライセンス（Ver.${escapeHtml(license.version || "1.10")}）に準拠します。
            基本条項（語の定義、免責、禁止行為、準拠法等）の詳細は
            <a href="${escapeHtml(config.vn3TermsUrl)}" target="_blank" rel="noopener noreferrer">VN3公式の本文・解説</a>
            および
            <a href="${escapeHtml(config.vn3OfficialUrl)}" target="_blank" rel="noopener noreferrer">vn3.org</a>
            を参照してください。
          </p>
          ${renderRaw(license)}
        </section>
      </article>`;
  }

  function renderIndex(licenses) {
    if (!licenses.length) {
      return `<div class="md-empty empty-state">
        <p class="eyebrow">License Viewer</p>
        <h1>ライセンス一覧</h1>
        <p>表示できるライセンスファイルがありません！</p>
      </div>`;
    }

    const cards = licenses
      .map((item) => {
        const href = `?product=${encodeURIComponent(item.id)}`;
        return `
          <a class="license-card" href="${href}">
            <span class="card-kicker">VN3</span>
            <strong>${escapeHtml(item.title || item.product)}</strong>
            <span class="card-path">${escapeHtml(item.relativePath)}</span>
          </a>`;
      })
      .join("");

    return `
      <section class="index-view">
        <header class="index-header">
          <p class="eyebrow">Samirin Booth License Viewer</p>
          <h1>ライセンス一覧</h1>
        </header>
        <div class="license-grid">${cards}</div>
      </section>`;
  }

  function renderError(message, detail) {
    return `
      <div class="md-empty empty-state error-state">
        <p class="eyebrow">Error</p>
        <h1>読み込みに失敗しました</h1>
        <p>${escapeHtml(message)}</p>
        ${detail ? `<pre class="raw-license">${escapeHtml(detail)}</pre>` : ""}
      </div>`;
  }

  global.LicenseRenderer = {
    renderDocument,
    renderIndex,
    renderError,
    escapeHtml,
  };
})(window);
