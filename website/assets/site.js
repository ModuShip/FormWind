(function () {
  "use strict";

  var root = document.documentElement;
  var texts = {
    en: { light: "Switch to light theme", dark: "Switch to dark theme", openNav: "Open navigation", closeNav: "Close navigation",
          copy: "Copy", copied: "Copied", onPage: "On this page", anchor: "Link to this section" },
    tr: { light: "Açık temaya geç", dark: "Koyu temaya geç", openNav: "Menüyü aç", closeNav: "Menüyü kapat",
          copy: "Kopyala", copied: "Kopyalandı", onPage: "Bu sayfada", anchor: "Bu bölümün bağlantısı" }
  };
  var t = texts[root.lang] || texts.en;

  // Theme toggle (theme.js has already set data-theme).
  var themeButton = document.getElementById("theme-toggle");
  function syncThemeButton() {
    var dark = root.getAttribute("data-theme") === "dark";
    if (themeButton) themeButton.setAttribute("aria-label", dark ? t.light : t.dark);
  }
  if (themeButton) {
    themeButton.addEventListener("click", function () {
      var next = root.getAttribute("data-theme") === "dark" ? "light" : "dark";
      root.setAttribute("data-theme", next);
      try { localStorage.setItem("fw-theme", next); } catch (e) { }
      syncThemeButton();
    });
  }
  syncThemeButton();

  // Mobile sidebar.
  var menuButton = document.getElementById("menu-toggle");
  var scrim = document.getElementById("scrim");
  function setNav(open) {
    document.body.classList.toggle("nav-open", open);
    if (menuButton) {
      menuButton.setAttribute("aria-expanded", String(open));
      menuButton.setAttribute("aria-label", open ? t.closeNav : t.openNav);
    }
    if (scrim) scrim.hidden = !open;
  }
  if (menuButton) menuButton.addEventListener("click", function () { setNav(!document.body.classList.contains("nav-open")); });
  if (scrim) scrim.addEventListener("click", function () { setNav(false); });
  document.addEventListener("keydown", function (e) { if (e.key === "Escape") setNav(false); });

  // Syntax highlighting for C#, JSON(C) and shell snippets. Each language is one regex with named groups.
  var grammars = {
    csharp: new RegExp([
      "(?<comment>\\/\\/.*|\\/\\*[\\s\\S]*?\\*\\/)",
      "(?<string>\\$?@?\"(?:[^\"\\\\]|\\\\.)*\"|'(?:[^'\\\\]|\\\\.)')",
      "(?<keyword>\\b(?:using|namespace|class|sealed|static|internal|public|private|readonly|void|var|new|if|else|return|foreach|in|for|while|true|false|null|this|base|string|int|bool|double|object|typeof|await|async|override|get|set|is|as|out|ref)\\b)",
      "(?<number>\\b\\d+(?:\\.\\d+)?[fFmMdD]?\\b)",
      "(?<fn>\\b[A-Z][A-Za-z0-9_]*(?=\\s*(?:<[^>]*>)?\\())",
      "(?<type>\\b[A-Z][A-Za-z0-9_]*\\b)"
    ].join("|"), "g"),
    json: new RegExp([
      "(?<comment>\\/\\/.*)",
      "(?<key>\"(?:[^\"\\\\]|\\\\.)*\"(?=\\s*:))",
      "(?<string>\"(?:[^\"\\\\]|\\\\.)*\")",
      "(?<keyword>\\b(?:true|false|null)\\b)",
      "(?<number>-?\\b\\d+(?:\\.\\d+)?\\b)"
    ].join("|"), "g"),
    shell: new RegExp([
      "(?<comment>(?:^|(?<=\\s))#.*)",
      "(?<string>\"(?:[^\"\\\\`]|\\\\.|`.)*\"|'[^']*')",
      "(?<type>\\$env:\\w+|\\$\\w+)",
      "(?<keyword>(?:^|(?<=\\s))--?[A-Za-z][\\w-]*)"
    ].join("|"), "gm")
  };
  var aliases = { cs: "csharp", csharp: "csharp", json: "json", jsonc: "json", bash: "shell", powershell: "shell", shell: "shell", ps: "shell" };

  function escapeHtml(text) {
    return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  }

  function highlight(code) {
    var match = /language-(\w+)/.exec(code.className);
    var grammar = match && grammars[aliases[match[1]]];
    if (!grammar) return;
    var text = code.textContent;
    var html = "";
    var last = 0;
    grammar.lastIndex = 0;
    var m;
    while ((m = grammar.exec(text)) !== null) {
      if (m[0] === "") { grammar.lastIndex++; continue; }
      html += escapeHtml(text.slice(last, m.index));
      var kind = "";
      for (var name in m.groups) { if (m.groups[name] !== undefined) { kind = name; break; } }
      html += '<span class="tk-' + kind + '">' + escapeHtml(m[0]) + "</span>";
      last = m.index + m[0].length;
    }
    code.innerHTML = html + escapeHtml(text.slice(last));
  }

  try {
    document.querySelectorAll("pre code[class*='language-']").forEach(highlight);
  } catch (e) {
    // Older engines without named groups or lookbehind show plain code.
  }

  // Copy buttons.
  function copyText(text, button) {
    var done = function () {
      var label = button.textContent;
      button.textContent = t.copied;
      setTimeout(function () { button.textContent = label; }, 1400);
    };
    if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(text).then(done, function () { });
    } else {
      var area = document.createElement("textarea");
      area.value = text;
      area.setAttribute("readonly", "");
      area.style.position = "fixed";
      area.style.opacity = "0";
      document.body.appendChild(area);
      area.select();
      try { document.execCommand("copy"); done(); } catch (e) { }
      area.remove();
    }
  }
  document.querySelectorAll("pre").forEach(function (pre) {
    var button = document.createElement("button");
    button.type = "button";
    button.className = "copy";
    button.textContent = t.copy;
    button.addEventListener("click", function () { copyText(pre.querySelector("code") ? pre.querySelector("code").textContent : pre.textContent, button); });
    pre.appendChild(button);
  });
  document.querySelectorAll("[data-copy]").forEach(function (button) {
    button.addEventListener("click", function () {
      var target = document.getElementById(button.getAttribute("data-copy"));
      if (target) copyText(target.textContent, button);
    });
  });

  // Heading anchors and the "On this page" list.
  var article = document.querySelector(".doc article");
  var toc = document.querySelector(".toc");
  if (article) {
    var used = {};
    var headings = article.querySelectorAll("h2, h3");
    headings.forEach(function (h) {
      if (!h.id) {
        var fold = { "ç": "c", "ğ": "g", "ı": "i", "ö": "o", "ş": "s", "ü": "u" };
        var base = h.textContent.toLocaleLowerCase(root.lang || "en").replace(/[çğıöşü]/g, function (c) { return fold[c]; })
          .replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || "section";
        var id = base, n = 2;
        while (used[id] || document.getElementById(id)) id = base + "-" + n++;
        h.id = id;
      }
      used[h.id] = true;
      h.setAttribute("data-label", h.textContent);
      var a = document.createElement("a");
      a.className = "anchor";
      a.href = "#" + h.id;
      a.setAttribute("aria-label", t.anchor);
      a.textContent = "#";
      h.appendChild(a);
    });

    var sections = article.querySelectorAll("h2");
    if (toc && sections.length > 1) {
      var title = document.createElement("p");
      title.textContent = t.onPage;
      var list = document.createElement("ul");
      var links = [];
      sections.forEach(function (h) {
        var li = document.createElement("li");
        var a = document.createElement("a");
        a.href = "#" + h.id;
        a.textContent = h.getAttribute("data-label");
        li.appendChild(a);
        list.appendChild(li);
        links.push(a);
      });
      toc.appendChild(title);
      toc.appendChild(list);

      var setActive = function () {
        var offset = 120, current = 0;
        sections.forEach(function (h, i) { if (h.getBoundingClientRect().top - offset <= 0) current = i; });
        links.forEach(function (a, i) { a.classList.toggle("active", i === current); });
      };
      window.addEventListener("scroll", setActive, { passive: true });
      setActive();
    }
  }

  // Close the mobile sidebar after following a link.
  document.querySelectorAll(".sidebar a").forEach(function (a) { a.addEventListener("click", function () { setNav(false); }); });

  // Tabs on the landing page (presets gallery).
  document.querySelectorAll("[role='tablist']").forEach(function (list) {
    var tabs = Array.prototype.slice.call(list.querySelectorAll("[role='tab']"));
    function select(tab, focus) {
      tabs.forEach(function (t) {
        var on = t === tab;
        t.setAttribute("aria-selected", String(on));
        t.tabIndex = on ? 0 : -1;
        var panel = document.getElementById(t.getAttribute("aria-controls"));
        if (panel) panel.hidden = !on;
      });
      if (focus) tab.focus();
    }
    tabs.forEach(function (tab, i) {
      tab.addEventListener("click", function () { select(tab, false); });
      tab.addEventListener("keydown", function (e) {
        var next = e.key === "ArrowRight" ? i + 1 : e.key === "ArrowLeft" ? i - 1 : e.key === "Home" ? 0 : e.key === "End" ? tabs.length - 1 : null;
        if (next === null) return;
        e.preventDefault();
        select(tabs[(next + tabs.length) % tabs.length], true);
      });
    });
  });
})();
