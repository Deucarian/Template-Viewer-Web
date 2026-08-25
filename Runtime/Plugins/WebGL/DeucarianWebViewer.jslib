mergeInto(LibraryManager.library, {
  $DeucarianWebViewerBrowserInterop: {
    getViewerConfig: function () {
      return window.deucarianWebViewerConfig
        || window.DeucarianWebViewerConfig
        || {};
    },

    normalizeExplicitOrigin: function (raw) {
      if (!raw || !raw.toString) {
        return null;
      }

      raw = raw.toString().trim();
      if (!raw || raw === "*") {
        return null;
      }

      try {
        var url = new URL(raw);
        var hasPath = url.pathname && url.pathname !== "/";
        if ((url.protocol !== "http:" && url.protocol !== "https:")
            || hasPath
            || !!url.search
            || !!url.hash
            || !!url.username
            || !!url.password) {
          return null;
        }

        return url.origin;
      } catch (error) {
        return null;
      }
    },

    resolveConfiguredParentOrigin: function () {
      var helper = DeucarianWebViewerBrowserInterop;
      var config = helper.getViewerConfig();
      return helper.normalizeExplicitOrigin(
        config.parentOrigin || config.parent_origin);
    }
  },

  DeucarianWebViewerIsParentIframe: function () {
    return typeof window !== "undefined"
      && !!window.parent
      && window.parent !== window
      ? 1
      : 0;
  },

  DeucarianWebViewerGetConfiguredParentOrigin__deps: [
    "$DeucarianWebViewerBrowserInterop"
  ],
  DeucarianWebViewerGetConfiguredParentOrigin: function () {
    if (typeof window === "undefined") {
      return stringToNewUTF8("");
    }

    var origin = DeucarianWebViewerBrowserInterop
      .resolveConfiguredParentOrigin();
    return stringToNewUTF8(origin || "");
  }
});
