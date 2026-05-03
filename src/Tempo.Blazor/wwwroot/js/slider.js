window.TempoSlider = {
    initRangeSlider: function (container) {
        const startInput = container.querySelector('.tm-range-slider__input--start');
        const endInput = container.querySelector('.tm-range-slider__input--end');
        if (!startInput || !endInput) return;

        container.addEventListener('pointerdown', function (e) {
            const rect = container.getBoundingClientRect();
            const x = (e.clientX - rect.left) / rect.width;

            const min = parseFloat(startInput.min);
            const max = parseFloat(startInput.max);
            const startPos = (parseFloat(startInput.value) - min) / (max - min);
            const endPos = (parseFloat(endInput.value) - min) / (max - min);

            if (Math.abs(x - startPos) < Math.abs(x - endPos)) {
                startInput.style.zIndex = '10';
                endInput.style.zIndex = '1';
            } else {
                startInput.style.zIndex = '1';
                endInput.style.zIndex = '10';
            }
        });
    }
};
