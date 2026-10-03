import { Component, inject, input, signal } from '@angular/core';
import { Auth } from '../core/auth';
import { toApiError } from '../core/api';

@Component({
  selector: 'app-login',
  template: `
    <div class="page narrow">
      <h1>Sign in</h1>
      <p class="muted">Enter your email and we will send a single-use sign-in link. The same account works for every exam. New addresses get an account once the link is used.</p>
      @if (sent()) {
        <div class="notice ok" role="status" aria-live="polite">
          <p><strong>Check your inbox.</strong> If the address is valid, a link is on its way. It works once and expires in 15 minutes.</p>
          <button type="button" class="btn quiet" (click)="sent.set(false)">Use a different address</button>
        </div>
      } @else {
        <form (submit)="submit($event)" novalidate>
          <label class="field" for="email">Email address
            <input id="email" name="email" type="email" autocomplete="email" required [value]="email()"
                   (input)="email.set($any($event.target).value)" [attr.aria-invalid]="!!error()" aria-describedby="email-error" />
          </label>
          <p id="email-error" class="err" role="alert">{{ error() }}</p>
          <button class="btn" type="submit" [disabled]="busy()">{{ busy() ? 'Sending link…' : 'Send sign-in link' }}</button>
        </form>
      }
    </div>
  `,
  styles: `.narrow { max-width: 560px; } form { display: grid; gap: 0.75rem; margin-top: 1.5rem; } .err { color: var(--wrong); min-height: 1.5em; margin: 0; }`,
})
export class LoginPage {
  private auth = inject(Auth);
  next = input<string>();
  protected email = signal('');
  protected busy = signal(false);
  protected sent = signal(false);
  protected error = signal('');

  async submit(e: Event) {
    e.preventDefault();
    const value = this.email().trim();
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) { this.error.set('Enter a valid email address, for example name@company.com.'); return; }
    this.error.set(''); this.busy.set(true);
    try {
      if (this.next()) sessionStorage.setItem('examprep.next', this.next()!);
      await this.auth.requestLink(value);
      this.sent.set(true);
    } catch (err) {
      const a = toApiError(err);
      this.error.set(a.status === 429 ? 'Too many requests. Wait a minute and try again.' : a.message);
    } finally { this.busy.set(false); }
  }
}
