import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Auth } from '../core/auth';
import { toApiError } from '../core/api';

@Component({
  selector: 'app-verify',
  imports: [RouterLink],
  template: `
    <div class="page">
      @if (error()) {
        <h1>That link did not work</h1>
        <div class="notice error" role="alert">{{ error() }}</div>
        <a class="btn" routerLink="/login">Request a new link</a>
      } @else {
        <h1>Signing you in…</h1>
        <p aria-live="polite" class="muted">Checking your link.</p>
      }
    </div>
  `,
})
export class VerifyPage implements OnInit {
  private auth = inject(Auth);
  private router = inject(Router);
  protected error = signal('');

  async ngOnInit() {
    // The token arrives in the URL fragment so it never reaches server logs. Remove it from history right away.
    const token = new URLSearchParams(location.hash.slice(1)).get('token');
    history.replaceState(null, '', location.pathname);
    if (!token) { this.error.set('The link is incomplete. Request a new one.'); return; }
    try {
      await this.auth.verify(token);
      const next = sessionStorage.getItem('examprep.next');
      sessionStorage.removeItem('examprep.next');
      this.router.navigateByUrl(next && next.startsWith('/') && !next.startsWith('//') ? next : '/my');
    } catch (e) {
      this.error.set(toApiError(e).message);
    }
  }
}
