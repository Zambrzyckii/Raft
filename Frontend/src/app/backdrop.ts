import { afterNextRender, Component, DestroyRef, ElementRef, inject, viewChild } from '@angular/core';

const RADIUS = 72;
const FADE_MS = 900;

@Component({
  selector: 'app-backdrop',
  template: '<canvas #canvas aria-hidden="true"></canvas>',
  styles: `
    canvas {
      position: fixed;
      inset: 0;
      z-index: -1;
      width: 100%;
      height: 100%;
      pointer-events: none;
    }
  `
})
export class Backdrop {
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  constructor() {
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => {
      if (!matchMedia('(prefers-reduced-motion: reduce), (hover: none)').matches) {
        destroyRef.onDestroy(afterglow(this.canvas().nativeElement));
      }
    });
  }
}

function afterglow(canvas: HTMLCanvasElement): () => void {
  const context = canvas.getContext('2d')!;
  const tokens = getComputedStyle(document.documentElement);
  const grid = parseFloat(tokens.getPropertyValue('--grid'));
  const color = tokens.getPropertyValue('--dim').trim();
  let ratio = 0;
  let columns = 0;
  let light = new Float32Array(0);
  let frame = 0;
  let last = 0;

  const resize = () => {
    ratio = devicePixelRatio;
    canvas.width = innerWidth * ratio;
    canvas.height = innerHeight * ratio;
    columns = Math.ceil(innerWidth / grid);
    light = new Float32Array(columns * Math.ceil(innerHeight / grid));
  };

  const draw = (now: number) => {
    const fade = (now - last) / FADE_MS;
    last = now;
    context.setTransform(ratio, 0, 0, ratio, 0, 0);
    context.clearRect(0, 0, innerWidth, innerHeight);
    context.fillStyle = color;
    let lit = false;
    for (let i = 0; i < light.length; i++) {
      if (light[i] > 0) {
        light[i] = Math.max(0, light[i] - fade);
        lit = true;
        context.globalAlpha = light[i];
        context.beginPath();
        context.arc((i % columns + 0.5) * grid, (Math.floor(i / columns) + 0.5) * grid, 1 + light[i], 0, 2 * Math.PI);
        context.fill();
      }
    }
    frame = lit ? requestAnimationFrame(draw) : 0;
  };

  const excite = (event: PointerEvent) => {
    if (devicePixelRatio !== ratio) {
      resize();
    }
    const rows = light.length / columns;
    const x0 = Math.max(0, Math.floor((event.clientX - RADIUS) / grid));
    const x1 = Math.min(columns - 1, Math.floor((event.clientX + RADIUS) / grid));
    const y0 = Math.max(0, Math.floor((event.clientY - RADIUS) / grid));
    const y1 = Math.min(rows - 1, Math.floor((event.clientY + RADIUS) / grid));
    for (let y = y0; y <= y1; y++) {
      for (let x = x0; x <= x1; x++) {
        const distance = Math.hypot((x + 0.5) * grid - event.clientX, (y + 0.5) * grid - event.clientY);
        light[y * columns + x] = Math.max(light[y * columns + x], 1 - distance / RADIUS);
      }
    }
    if (!frame) {
      last = performance.now();
      frame = requestAnimationFrame(draw);
    }
  };

  resize();
  addEventListener('resize', resize);
  document.addEventListener('pointermove', excite, { passive: true });
  return () => {
    removeEventListener('resize', resize);
    document.removeEventListener('pointermove', excite);
    cancelAnimationFrame(frame);
  };
}
