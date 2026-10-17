// Runs in <head> before the page paints, so a saved theme does not flash the other one first.
(function () {
  var theme = null;
  try { theme = localStorage.getItem("fw-theme"); } catch (e) { }
  if (theme !== "light" && theme !== "dark") {
    theme = window.matchMedia && matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  }
  document.documentElement.setAttribute("data-theme", theme);
})();
