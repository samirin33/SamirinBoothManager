(function () {
  const config = window.LicenseViewerConfig;
  const parser = window.LicenseParser;
  const renderer = window.LicenseRenderer;

  const els = {
    app: document.getElementById("app"),
    crumb: document.getElementById("breadcrumb"),
    status: document.getElementById("load-status"),
    updated: document.getElementById("last-updated"),
  };

  function formatUpdatedAt(value) {
    if (!value) return "";
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return String(value);
    try {
      return new Intl.DateTimeFormat("ja-JP", {
        year: "numeric",
        month: "long",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        hour12: false,
        timeZone: "Asia/Tokyo",
      }).format(date);
    } catch (_error) {
      return date.toLocaleString("ja-JP");
    }
  }

  function setLastUpdated(value) {
    if (!els.updated) return;
    const formatted = formatUpdatedAt(value);
    els.updated.textContent = formatted
      ? `最終更新日: ${formatted}`
      : "最終更新日: —";
  }

  function setStatus(text, isError = false) {
    if (!els.status) return;
    els.status.textContent = text || "";
    els.status.classList.toggle("is-error", Boolean(isError));
  }

  function queryParams() {
    const params = new URLSearchParams(window.location.search);
    return {
      product: params.get("product") || params.get("p") || "",
      file: params.get("file") || params.get("path") || "",
      url: params.get("url") || "",
    };
  }

  async function fetchText(url) {
    const response = await fetch(url, { cache: "no-cache" });
    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText} @ ${url}`);
    }
    return response.text();
  }

  async function fetchFirstAvailable(urls) {
    const errors = [];
    for (const url of urls.filter(Boolean)) {
      try {
        const text = await fetchText(url);
        return { text, url };
      } catch (error) {
        errors.push(String(error.message || error));
      }
    }
    throw new Error(errors.join("\n"));
  }

  function githubRawUrl(relativePath) {
    const gh = config.github || {};
    if (!gh.owner || !gh.repo || !relativePath) return "";
    const base = `https://raw.githubusercontent.com/${gh.owner}/${gh.repo}/${gh.branch || "main"}`;
    const info = (gh.informationPath || "").replace(/^\/|\/$/g, "");
    return `${base}/${info}/${relativePath}`.replace(/([^:]\/)\/+/g, "$1");
  }

  async function loadManifest() {
    try {
      const text = await fetchText(config.manifestUrl);
      const data = JSON.parse(text);
      return {
        generatedAt: data.generatedAt || "",
        licenses: Array.isArray(data.licenses) ? data.licenses : [],
      };
    } catch (error) {
      console.warn("manifest load failed", error);
      return { generatedAt: "", licenses: [] };
    }
  }

  function resolveUpdatedAt(manifest, entry) {
    const candidates = [
      entry?.updatedAt,
      manifest.generatedAt,
    ].filter(Boolean);
    if (!candidates.length) return "";
    return candidates
      .map((value) => new Date(value))
      .filter((date) => !Number.isNaN(date.getTime()))
      .sort((a, b) => b - a)[0];
  }

  function resolveIndexUpdatedAt(manifest) {
    const dates = [
      manifest.generatedAt,
      ...manifest.licenses.map((item) => item.updatedAt),
    ]
      .filter(Boolean)
      .map((value) => new Date(value))
      .filter((date) => !Number.isNaN(date.getTime()));
    if (!dates.length) return "";
    return dates.sort((a, b) => b - a)[0];
  }

  function findEntry(manifest, params) {
    if (params.product) {
      const key = params.product.toLowerCase();
      return (
        manifest.find((item) => item.id.toLowerCase() === key) ||
        manifest.find((item) => item.product.toLowerCase() === key) ||
        null
      );
    }
    if (params.file) {
      const normalized = params.file.replace(/^\/+/, "").toLowerCase();
      return (
        manifest.find(
          (item) =>
            item.relativePath.toLowerCase() === normalized ||
            item.localPath.toLowerCase() === normalized ||
            `${item.product}/${item.fileName}`.toLowerCase() === normalized
        ) || null
      );
    }
    return null;
  }

  function buildCandidateUrls(entry, params) {
    if (params.url) return [params.url];

    const list = [];
    if (entry && typeof config.fetchCandidates === "function") {
      list.push(...config.fetchCandidates(entry));
    }
    if (entry) {
      list.push(githubRawUrl(entry.relativePath));
    }
    if (params.file) {
      list.push(`licenses/${params.file}`);
      list.push(`../SamirinBoothInformation/${params.file}`);
      list.push(githubRawUrl(params.file));
    }
    if (params.product && !entry) {
      const rel = `${params.product}/VN3License.txt`;
      list.push(`licenses/${rel}`);
      list.push(`../SamirinBoothInformation/${rel}`);
      list.push(githubRawUrl(rel));
    }
    return [...new Set(list.filter(Boolean))];
  }

  function setBreadcrumb(parts) {
    if (!els.crumb) return;
    els.crumb.innerHTML = parts
      .map((part, index) => {
        if (part.href && index < parts.length - 1) {
          return `<a href="${part.href}">${renderer.escapeHtml(part.label)}</a>`;
        }
        return `<span>${renderer.escapeHtml(part.label)}</span>`;
      })
      .join('<span class="sep">/</span>');
  }

  async function showIndex(manifest) {
    setBreadcrumb([{ label: "Licenses", href: "./" }]);
    els.app.innerHTML = renderer.renderIndex(manifest.licenses);
    setLastUpdated(resolveIndexUpdatedAt(manifest));
    document.title = "ライセンス一覧 | Samirin Booth";
  }

  async function showLicense(manifest, params) {
    const entry = findEntry(manifest.licenses, params);
    const productName =
      entry?.title ||
      entry?.product ||
      params.product ||
      params.file ||
      "License";

    setBreadcrumb([
      { label: "Licenses", href: "./" },
      { label: productName },
    ]);
    setStatus("ライセンスを読み込み中…");

    const candidates = buildCandidateUrls(entry, params);
    let loaded;
    try {
      loaded = await fetchFirstAvailable(candidates);
    } catch (error) {
      els.app.innerHTML = renderer.renderError(
        "ライセンスファイルを取得できませんでした。パスや GitHub Pages の公開設定を確認してください。",
        String(error.message || error)
      );
      setStatus("読み込み失敗", true);
      setLastUpdated(resolveUpdatedAt(manifest, entry));
      return;
    }

    const license = parser.parseLicenseText(loaded.text, {
      product: entry?.product || params.product,
      title: entry?.title || entry?.product || params.product,
    });

    els.app.innerHTML = renderer.renderDocument(license, config);
    setStatus(`取得元: ${loaded.url}`);
    setLastUpdated(resolveUpdatedAt(manifest, entry));
    document.title = `${license.title} 利用規約 | Samirin Booth`;
  }

  async function main() {
    const params = queryParams();
    const manifest = await loadManifest();

    if (params.product || params.file || params.url) {
      await showLicense(manifest, params);
    } else {
      await showIndex(manifest);
    }
  }

  main().catch((error) => {
    els.app.innerHTML = renderer.renderError(
      "ページ初期化に失敗しました。",
      String(error.message || error)
    );
    setStatus("初期化失敗", true);
  });
})();
