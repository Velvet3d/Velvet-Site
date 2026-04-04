// Canvas resolution helpers for fixing pixelation

const canvasResizeBindings = new Map();
let nextResizeBindingId = 1;

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
    },

    bindResizeTracking: function (canvas, dotNetRef, methodName) {
        if (!(canvas instanceof HTMLCanvasElement)) {
            throw new Error("CanvasHelpers.bindResizeTracking: provided element is not a canvas");
        }

        if (!dotNetRef || typeof dotNetRef.invokeMethodAsync !== "function") {
            throw new Error("CanvasHelpers.bindResizeTracking: dotNetRef is invalid");
        }

        const invokeResize = () => {
            const rect = canvas.getBoundingClientRect();
            const width = Math.max(1, Math.round(rect.width));
            const height = Math.max(1, Math.round(rect.height));
            const dpr = window.devicePixelRatio || 1.0;

            return dotNetRef.invokeMethodAsync(methodName, width, height, dpr);
        };

        const onWindowResize = () => {
            invokeResize().catch(() => {
                // Ignore late callbacks during teardown.
            });
        };

        window.addEventListener("resize", onWindowResize);

        const bindingId = `resize-${nextResizeBindingId++}`;
        canvasResizeBindings.set(bindingId, onWindowResize);

        invokeResize().catch(() => {
            // Ignore late callbacks during teardown.
        });

        return bindingId;
    },

    unbindResizeTracking: function (bindingId) {
        const onWindowResize = canvasResizeBindings.get(bindingId);
        if (!onWindowResize) {
            return;
        }

        window.removeEventListener("resize", onWindowResize);
        canvasResizeBindings.delete(bindingId);
    }
};
