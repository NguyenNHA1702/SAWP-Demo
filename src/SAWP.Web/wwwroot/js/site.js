// SAWP Playful Geometric Client Script

document.addEventListener('DOMContentLoaded', () => {
  // 1. Toast notifications auto-dismiss
  const toasts = document.querySelectorAll('.toast');
  toasts.forEach(t => {
    setTimeout(() => {
      t.style.transition = 'all 0.4s ease';
      t.style.opacity = '0';
      t.style.transform = 'translateY(20px)';
      setTimeout(() => t.remove(), 400);
    }, 4500);
  });

  // 2. Demo Account Cards Autofill & Double-Click Login
  const demoCards = document.querySelectorAll('.demo-account-card');
  const loginInput = document.getElementById('loginUsername');
  const passInput = document.getElementById('loginPassword');
  const loginForm = document.getElementById('loginForm');

  demoCards.forEach(card => {
    const uname = card.getAttribute('data-username');
    const pwd = card.getAttribute('data-password') || 'Demo@123';

    card.addEventListener('click', (e) => {
      e.preventDefault();
      if (loginInput && passInput) {
        loginInput.value = uname;
        passInput.value = pwd;
        loginInput.focus();

        // Highlight card briefly
        demoCards.forEach(c => c.classList.remove('selected-card'));
        card.classList.add('selected-card');
      }
    });

    card.addEventListener('dblclick', (e) => {
      e.preventDefault();
      if (loginInput && passInput && loginForm) {
        loginInput.value = uname;
        passInput.value = pwd;
        loginForm.submit();
      }
    });
  });

  // 3. Tab switching
  const tabBtns = document.querySelectorAll('.tab-btn');
  tabBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      const targetId = btn.getAttribute('data-tab');
      const container = btn.closest('.tabs-container');
      if (!container) return;

      container.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
      container.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));

      btn.classList.add('active');
      const pane = container.querySelector(`#${targetId}`);
      if (pane) pane.classList.add('active');
    });
  });
});
