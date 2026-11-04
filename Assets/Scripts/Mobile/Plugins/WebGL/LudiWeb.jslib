// Puente JavaScript para la build web (lo usa WebPlatform.cs). Unity lo incluye solo en las builds WebGL.
// Detección de pantalla táctil, orientación, pantalla completa y resolución interna del canvas.
mergeInto(LibraryManager.library, {

  // Puntero principal "grueso" = el dedo (móvil / tableta). Los iPad modernos se presentan como Mac:
  // se reconocen porque tienen varios puntos táctiles.
  LudiWeb_IsTouchPrimary: function () {
    try {
      if (window.matchMedia && window.matchMedia('(pointer: coarse)').matches) return 1;
      if (navigator.maxTouchPoints > 1 && /Macintosh/.test(navigator.userAgent)) return 1;
    } catch (e) {}
    return 0;
  },

  LudiWeb_HasTouch: function () {
    return ('ontouchstart' in window) || navigator.maxTouchPoints > 0 ? 1 : 0;
  },

  // Se mira la VENTANA, no el canvas: la página mantiene el canvas apaisado aunque el móvil esté vertical
  LudiWeb_IsPortrait: function () {
    return window.innerHeight > window.innerWidth ? 1 : 0;
  },

  // El iPhone (Safari) no deja poner pantalla completa a una página
  LudiWeb_FullscreenSupported: function () {
    var d = document;
    return (d.fullscreenEnabled || d.webkitFullscreenEnabled) ? 1 : 0;
  },

  LudiWeb_DevicePixelRatio: function () {
    return window.devicePixelRatio || 1;
  },

  LudiWeb_CanvasCssWidth: function () {
    var c = Module.canvas;
    return c && c.clientWidth ? c.clientWidth : window.innerWidth;
  },

  LudiWeb_CanvasCssHeight: function () {
    var c = Module.canvas;
    return c && c.clientHeight ? c.clientHeight : window.innerHeight;
  },

  // Resolución interna del canvas = tamaño CSS x ratio. f = 0 -> automática (como mucho x2, para que
  // los móviles con pantallas x3 no vayan lentos). Se guarda en localStorage para que la página la use
  // ya al arrancar (antes de que Unity cargue los ajustes).
  LudiWeb_SetRenderScale: function (f) {
    try { localStorage.setItem('ludi_render_scale', String(f)); } catch (e) {}
    var dpr = window.devicePixelRatio || 1;
    var r = f > 0 ? dpr * f : Math.min(dpr, 2);
    Module.devicePixelRatio = Math.max(0.5, r);
  },

  // Al entrar en pantalla completa en un móvil/tableta, se bloquea en horizontal (Android; iOS lo ignora)
  LudiWeb_Init: function () {
    if (window.__ludiWebInit) return;
    window.__ludiWebInit = true;
    function lock() {
      var fs = document.fullscreenElement || document.webkitFullscreenElement;
      var coarse = window.matchMedia && window.matchMedia('(pointer: coarse)').matches;
      if (fs && coarse && screen.orientation && screen.orientation.lock)
        screen.orientation.lock('landscape').catch(function () {});
    }
    document.addEventListener('fullscreenchange', lock);
    document.addEventListener('webkitfullscreenchange', lock);
    // Sin esto, una pulsación larga abre el menú contextual del navegador encima del juego
    if (Module.canvas) Module.canvas.addEventListener('contextmenu', function (e) { e.preventDefault(); });
  }
});
