/* Deck engine (shared with the ValidatorHub deck): navigation, 1920x1080 scaling, generated artwork and the brand intro. */
(function () {
  "use strict";

  var stage = document.getElementById("stage");
  var slides = Array.prototype.slice.call(stage.querySelectorAll(".slide"));
  var total = slides.length;
  var progress = document.getElementById("progress");
  var count = document.getElementById("count");
  var announcer = document.getElementById("announcer");
  var controls = document.getElementById("controls");
  var hint = document.getElementById("hint");
  var intro = document.getElementById("intro");
  var reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  var current = -1;
  var introTimers = [];
  var introRunning = false;

  /* ---------- Scale the fixed canvas to the viewport ---------- */

  function fit() {
    var scale = Math.min(window.innerWidth / 1920, window.innerHeight / 1080);
    stage.style.setProperty("--scale", String(scale));
  }

  /* ---------- Seeded random, so the artwork is identical on every load ---------- */

  function mulberry32(seed) {
    return function () {
      seed |= 0;
      seed = (seed + 0x6d2b79f5) | 0;
      var t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  /* ---------- Abstract facets (Brand Book p.39: abstract images, brand palette) ---------- */

  // Dark-to-light ramp around ERNI Dark Blue (same ramp as the app theme) ending in ERNI Light Blue.
  var PALETTE = ["#000a22", "#00102d", "#001638", "#001c44", "#012351", "#01295e", "#02306b", "#033778", "#2d5c9f", "#517ec0", "#00aadb"];

  function buildAbstract(el) {
    var w = Number(el.dataset.w);
    var h = Number(el.dataset.h);
    var rand = mulberry32(Number(el.dataset.seed || 1));
    var cell = 120;
    var cols = Math.ceil(w / cell) + 2;
    var rows = Math.ceil(h / cell) + 2;
    var pts = [];
    for (var r = 0; r < rows; r++) {
      pts.push([]);
      for (var c = 0; c < cols; c++) {
        var jx = r === 0 || r === rows - 1 ? 0 : (rand() - 0.5) * cell * 0.5;
        var jy = c === 0 || c === cols - 1 ? 0 : (rand() - 0.5) * cell * 0.5;
        pts[r].push([(c - 1) * cell + jx, (r - 1) * cell + jy]);
      }
    }
    var hx = w * 0.72;
    var hy = h * 0.78;
    var diag = Math.sqrt(w * w + h * h);
    var out = [];
    function tri(a, b, d, lift) {
      var cx = (a[0] + b[0] + d[0]) / 3;
      var cy = (a[1] + b[1] + d[1]) / 3;
      var dist = Math.sqrt((cx - hx) * (cx - hx) + (cy - hy) * (cy - hy)) / diag;
      var v = 0.98 - dist * 1.55 + lift + (rand() - 0.5) * 0.3;
      v = Math.max(0, Math.min(0.999, v));
      var fill = PALETTE[Math.floor(v * PALETTE.length)];
      out.push(
        '<polygon points="' +
          a[0].toFixed(1) + "," + a[1].toFixed(1) + " " +
          b[0].toFixed(1) + "," + b[1].toFixed(1) + " " +
          d[0].toFixed(1) + "," + d[1].toFixed(1) +
          '" fill="' + fill + '" stroke="' + fill + '" stroke-width="0.8"/>'
      );
    }
    for (var rr = 0; rr < rows - 1; rr++) {
      for (var cc = 0; cc < cols - 1; cc++) {
        var p00 = pts[rr][cc];
        var p01 = pts[rr][cc + 1];
        var p10 = pts[rr + 1][cc];
        var p11 = pts[rr + 1][cc + 1];
        if ((rr + cc) % 2 === 0) {
          tri(p00, p01, p11, 0.07);
          tri(p00, p11, p10, -0.05);
        } else {
          tri(p00, p01, p10, 0.07);
          tri(p01, p11, p10, -0.05);
        }
      }
    }
    var svg =
      '<svg viewBox="0 0 ' + w + " " + h + '" preserveAspectRatio="xMidYMid slice" aria-hidden="true">' +
      out.join("") +
      "</svg>";
    el.insertAdjacentHTML("afterbegin", svg);
  }

  /* ---------- Generated content ---------- */

  function list(attr) {
    return attr ? attr.split(",").map(Number) : [];
  }

  function buildDots(el) {
    var n = Number(el.dataset.dots);
    var off = list(el.dataset.off);
    var drop = list(el.dataset.drop);
    var html = "";
    var j = 0;
    for (var i = 0; i < n; i++) {
      if (off.indexOf(i) !== -1) {
        html += '<span class="dot off"></span>';
      } else if (drop.indexOf(i) !== -1) {
        html += '<span class="dot off drop" style="--j:' + j++ + '"></span>';
      } else {
        html += '<span class="dot"></span>';
      }
    }
    el.innerHTML = html;
    el.setAttribute("aria-hidden", "true");
  }

  function buildRanked(el) {
    var n = Number(el.dataset.rank);
    var html = "";
    for (var i = 0; i < n; i++) html += '<li style="--j:' + i + '">' + (i + 1) + "</li>";
    el.innerHTML = html;
    el.setAttribute("aria-hidden", "true");
  }

  function buildBuckets(el) {
    var n = Number(el.dataset.buckets);
    var hit = Number(el.dataset.hit);
    var html = "";
    for (var i = 0; i < n; i++) {
      var cls = i < hit ? "seg on" : i === hit ? "seg hit" : "seg";
      html += '<span class="' + cls + '" style="--j:' + i + '"></span>';
    }
    el.innerHTML = html;
    el.setAttribute("aria-hidden", "true");
  }

  function buildMarquee(el) {
    var items = el.dataset.marquee.split(",");
    var chips = items
      .map(function (t) {
        return '<span class="chip">' + t.replace(/&/g, "&amp;").replace(/</g, "&lt;") + "</span>";
      })
      .join("");
    // Two copies for a seamless loop; the second is hidden from assistive tech.
    el.innerHTML = '<span class="chips" style="flex-wrap:nowrap">' + chips + '</span><span class="chips" style="flex-wrap:nowrap" aria-hidden="true">' + chips + "</span>";
  }

  /* ---------- Count-up numbers ---------- */

  function countUp(slide) {
    var els = slide.querySelectorAll("[data-count]");
    Array.prototype.forEach.call(els, function (el) {
      var to = Number(el.dataset.count);
      if (reduceMotion.matches || to <= 1) {
        el.textContent = String(to);
        return;
      }
      var start = null;
      var duration = 1400;
      var delay = 350;
      el.textContent = "0";
      function step(ts) {
        if (!slide.classList.contains("is-active")) {
          el.textContent = String(to);
          return;
        }
        if (start === null) start = ts + delay;
        var t = Math.min(1, Math.max(0, (ts - start) / duration));
        var eased = 1 - Math.pow(1 - t, 3);
        el.textContent = String(Math.round(to * eased));
        if (t < 1) requestAnimationFrame(step);
      }
      requestAnimationFrame(step);
    });
  }

  function finalizeCounters() {
    Array.prototype.forEach.call(document.querySelectorAll("[data-count]"), function (el) {
      el.textContent = el.dataset.count;
    });
  }

  /* ---------- Navigation ---------- */

  function pad(n) {
    return n < 10 ? "0" + n : String(n);
  }

  function go(index, opts) {
    index = Math.max(0, Math.min(total - 1, index));
    if (index === current && !(opts && opts.force)) return;
    var prev = slides[current];
    if (prev) {
      prev.classList.remove("is-active");
      prev.setAttribute("aria-hidden", "true");
    }
    current = index;
    var slide = slides[current];
    slide.classList.add("is-active");
    slide.removeAttribute("aria-hidden");
    countUp(slide);

    progress.style.setProperty("--progress", String((current + 1) / total));
    count.textContent = pad(current + 1) + " / " + total;
    announcer.textContent = "Diapositiva " + (current + 1) + " de " + total + ": " + (slide.dataset.title || "");
    var hash = "#" + (current + 1);
    if (location.hash !== hash) history.replaceState(null, "", hash);
  }

  function next() {
    go(current + 1);
  }
  function prev() {
    go(current - 1);
  }

  function indexFromHash() {
    var n = parseInt(location.hash.replace("#", ""), 10);
    return isNaN(n) ? 0 : n - 1;
  }

  /* ---------- Intro: draw the coat of arms, reveal the logo, iris out (Brand Book p.127) ---------- */

  function endIntro() {
    if (!introRunning) return;
    introRunning = false;
    introTimers.forEach(clearTimeout);
    introTimers = [];
    intro.hidden = true;
    intro.classList.remove("play");
    if (current !== 0) go(0, { force: true });
  }

  function playIntro() {
    introRunning = true;
    intro.hidden = false;
    // Force a style flush so the animation starts from its first frame.
    void intro.offsetWidth;
    intro.classList.add("play");
    introTimers.push(
      setTimeout(function () {
        go(0, { force: true });
      }, 3050)
    );
    introTimers.push(setTimeout(endIntro, 3950));
  }

  /* ---------- Chrome: controls idle state, hint, fullscreen ---------- */

  var idleTimer;
  function wake() {
    controls.classList.remove("idle");
    clearTimeout(idleTimer);
    idleTimer = setTimeout(function () {
      controls.classList.add("idle");
    }, 2600);
  }

  function dismissHint() {
    hint.classList.add("gone");
  }

  function toggleFullscreen() {
    if (!document.fullscreenElement) {
      if (document.documentElement.requestFullscreen) document.documentElement.requestFullscreen();
    } else if (document.exitFullscreen) {
      document.exitFullscreen();
    }
  }

  /* ---------- Init ---------- */

  function init() {
    slides.forEach(function (slide, i) {
      slide.setAttribute("role", "group");
      slide.setAttribute("aria-roledescription", "diapositiva");
      slide.setAttribute("aria-label", i + 1 + " de " + total + ": " + (slide.dataset.title || ""));
      slide.setAttribute("aria-hidden", "true");
      var pg = slide.querySelector(".pg");
      if (pg) pg.textContent = pad(i + 1);
    });

    Array.prototype.forEach.call(document.querySelectorAll("[data-abstract]"), buildAbstract);
    Array.prototype.forEach.call(document.querySelectorAll("[data-dots]"), buildDots);
    Array.prototype.forEach.call(document.querySelectorAll("[data-rank]"), buildRanked);
    Array.prototype.forEach.call(document.querySelectorAll("[data-buckets]"), buildBuckets);
    Array.prototype.forEach.call(document.querySelectorAll("[data-marquee]"), buildMarquee);

    fit();
    window.addEventListener("resize", fit);

    var start = indexFromHash();
    if (start === 0 && !reduceMotion.matches) {
      playIntro();
    } else {
      go(start);
    }

    document.addEventListener("keydown", function (e) {
      if (e.metaKey || e.ctrlKey || e.altKey) return;
      if (introRunning) {
        endIntro();
        e.preventDefault();
        return;
      }
      switch (e.key) {
        case "ArrowRight":
        case "ArrowDown":
        case "PageDown":
        case " ":
        case "Enter":
          if (e.target.tagName === "BUTTON" && (e.key === " " || e.key === "Enter")) return;
          next();
          break;
        case "ArrowLeft":
        case "ArrowUp":
        case "PageUp":
        case "Backspace":
          prev();
          break;
        case "Home":
          go(0);
          break;
        case "End":
          go(total - 1);
          break;
        case "f":
        case "F":
          toggleFullscreen();
          break;
        case "p":
        case "P":
          finalizeCounters();
          window.print();
          break;
        default:
          return;
      }
      dismissHint();
      e.preventDefault();
    });

    document.getElementById("next").addEventListener("click", next);
    document.getElementById("prev").addEventListener("click", prev);
    document.getElementById("fs").addEventListener("click", toggleFullscreen);

    stage.addEventListener("click", function () {
      if (introRunning) endIntro();
    });

    var touchX = null;
    var touchY = null;
    stage.addEventListener(
      "touchstart",
      function (e) {
        touchX = e.touches[0].clientX;
        touchY = e.touches[0].clientY;
      },
      { passive: true }
    );
    stage.addEventListener("touchend", function (e) {
      if (introRunning) {
        endIntro();
        return;
      }
      if (touchX === null) return;
      var dx = e.changedTouches[0].clientX - touchX;
      var dy = e.changedTouches[0].clientY - touchY;
      if (Math.abs(dx) > 50 && Math.abs(dx) > Math.abs(dy)) {
        if (dx < 0) next();
        else prev();
        dismissHint();
      }
      touchX = null;
    });

    window.addEventListener("hashchange", function () {
      go(indexFromHash());
    });
    window.addEventListener("beforeprint", finalizeCounters);
    document.addEventListener("mousemove", wake);
    wake();
    setTimeout(dismissHint, 6000);
  }

  init();
})();
