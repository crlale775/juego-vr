mergeInto(LibraryManager.library, {

  _vrbbAlpha: 0,
  _vrbbBeta: 0,
  _vrbbGamma: 0,
  _vrbbSupported: 0,
  _vrbbHasData: 0,

  VRBB_OrientationSupported: function () {
    return (window.DeviceOrientationEvent !== undefined) ? 1 : 0;
  },

  VRBB_RequestOrientationPermission: function () {
    try {
      var D = window.DeviceOrientationEvent;
      if (D && typeof D.requestPermission === 'function') {
        D.requestPermission().then(function (state) {
          if (state === 'granted') { VRBB_AttachOrientation(); }
        }).catch(function () { });
      } else {
        VRBB_AttachOrientation();
      }
    } catch (e) { }
  },

  // Devuelve 1 solo cuando ya llegó al menos una lectura del sensor.
  // Sin esto, un navegador que expone la API pero no tiene sensores
  // (escritorio) daria una vista girada.
  VRBB_HasData: function () { return _vrbbHasData; },

  VRBB_GetAlpha: function () { return _vrbbAlpha; },
  VRBB_GetBeta: function () { return _vrbbBeta; },
  VRBB_GetGamma: function () { return _vrbbGamma; },

  VRBB_GetScreenAngle: function () {
    try {
      if (screen.orientation && typeof screen.orientation.angle === 'number') {
        return screen.orientation.angle;
      }
      if (typeof window.orientation === 'number') {
        return window.orientation;
      }
    } catch (e) { }
    return 0;
  }
});

function VRBB_AttachOrientation() {
  window.addEventListener('deviceorientation', function (e) {
    if (e.alpha === null && e.beta === null && e.gamma === null) {
      _vrbbSupported = 0;
      return;
    }
    _vrbbSupported = 1;
    _vrbbHasData = 1;
    _vrbbAlpha = e.alpha || 0;
    _vrbbBeta = e.beta || 0;
    _vrbbGamma = e.gamma || 0;
  }, true);
}
