// Canvas resolution helpers for fixing pixelation

window.CanvasHelpers = {
    getCanvasRect: function (canvas) {
        const rect = canvas.getBoundingClientRect();
        return {
            width: rect.width,
            height: rect.height
        };
    },

    getDevicePixelRatio: function () {
        return window.devicePixelRatio || 1.0;
    },

    setCanvasResolution: function (canvas, width, height) {
        canvas.width = width;
        canvas.height = height;
    }
};
