(function () {
  'use strict';

  var prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // Scroll reveal
  var revealEls = document.querySelectorAll('.reveal');
  if ('IntersectionObserver' in window && !prefersReducedMotion) {
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add('is-visible');
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.15 });
    revealEls.forEach(function (el) { observer.observe(el); });
  } else {
    revealEls.forEach(function (el) { el.classList.add('is-visible'); });
  }

  // Active nav link on scroll
  var sections = document.querySelectorAll('main .section[id]');
  var navLinks = document.querySelectorAll('.nav-link');

  function setActive(id) {
    navLinks.forEach(function (link) {
      link.classList.toggle('active', link.dataset.section === id);
    });
  }

  if ('IntersectionObserver' in window) {
    var navObserver = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          var id = entry.target.id;
          if (document.querySelector('.nav-link[data-section="' + id + '"]')) {
            setActive(id);
          }
        }
      });
    }, { rootMargin: '-40% 0px -50% 0px' });

    sections.forEach(function (section) { navObserver.observe(section); });
  }

  // Parallax effect
  var parallaxEls = document.querySelectorAll('.parallax');
  if (parallaxEls.length && !prefersReducedMotion) {
    var ticking = false;
    function updateParallax() {
      var viewportCenter = window.innerHeight / 2;
      parallaxEls.forEach(function (el) {
        var rect = el.getBoundingClientRect();
        var speed = parseFloat(el.dataset.speed) || 0.1;
        var offset = (rect.top + rect.height / 2 - viewportCenter) * speed;
        var img = el.querySelector('img');
        if (img) {
          img.style.transform = 'translateY(' + offset + 'px)';
        }
      });
      ticking = false;
    }
    window.addEventListener('scroll', function () {
      if (!ticking) {
        window.requestAnimationFrame(updateParallax);
        ticking = true;
      }
    });
    updateParallax();
  }

  // Contact form (no backend yet)
  var form = document.querySelector('.form-contacto');
  if (form) {
    form.addEventListener('submit', function (e) {
      e.preventDefault();
      alert('Gracias por tu mensaje. En esta versión de la página, el envío aún no está conectado a un servidor.');
      form.reset();
    });
  }

  // Lightbox para diagramas
  var lb = document.getElementById('lightbox');
  if (lb) {
    var lbImg = lb.querySelector('img');
    document.querySelectorAll('.func-img').forEach(function (btn) {
      btn.addEventListener('click', function () {
        var im = btn.querySelector('img');
        lbImg.src = im.src; lbImg.alt = im.alt;
        lb.hidden = false;
        lb.querySelector('.lightbox-cerrar').focus();
      });
    });
    function closeLb() { lb.hidden = true; lbImg.src = ''; }
    lb.addEventListener('click', closeLb);
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !lb.hidden) closeLb(); });
  }

  // Control de fuerza interactivo
  var rango = document.getElementById('fuerza-rango');
  var ftxt = document.getElementById('fuerza-txt');
  if (rango && ftxt) {
    function fuerza() {
      var v = +rango.value;
      ftxt.textContent = v < 34 ? 'Apretar suave: para un vaso, el servomotor aprieta poco.'
        : v > 66 ? 'Apretar fuerte: para una bolsa, el servomotor aprieta más.'
        : 'Nivel intermedio: el servomotor aprieta hasta el límite elegido.';
    }
    rango.addEventListener('input', fuerza); fuerza();
  }
})();
