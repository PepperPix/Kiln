document.addEventListener("DOMContentLoaded", async () => {
  const root = document.querySelector("#search[data-pagefind-root]");
  if (!root) {
    return;
  }

  const moduleUrl = root.getAttribute("data-pagefind-module") || "/pagefind/pagefind.js";

  try {
    const pagefindModule = await import(moduleUrl);
    const pagefind = pagefindModule.default ?? pagefindModule;
    if (!pagefind || typeof pagefind.init !== "function") {
      return;
    }

    const searchInput = document.createElement("input");
    searchInput.type = "search";
    searchInput.placeholder = "Search...";
    searchInput.setAttribute("aria-label", "Search site");
    root.appendChild(searchInput);

    const results = document.createElement("div");
    results.className = "pagefind-results";
    root.appendChild(results);

    await pagefind.init();

    searchInput.addEventListener("input", async (event) => {
      const query = event.target.value.trim();
      if (!query) {
        results.innerHTML = "";
        return;
      }

      const hits = await pagefind.search(query);
      if (!hits || !Array.isArray(hits.results)) {
        results.innerHTML = "";
        return;
      }

      const markup = await Promise.all(
        hits.results.slice(0, 5).map(async (hit) => {
          const meta = await hit.element();
          return `<a href="${meta.url}">${meta.title || hit.meta?.title || "Untitled"}</a>`;
        })
      );

      results.innerHTML = markup.join("");
    });
  } catch (error) {
    console.warn("Pagefind search failed to initialize", error);
  }
});
