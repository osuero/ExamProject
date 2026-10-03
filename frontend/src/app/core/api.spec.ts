import { HttpErrorResponse } from '@angular/common/http';
import { toApiError } from './api';

describe('toApiError', () => {
  it('maps a network failure to a clear message that keeps answers', () => {
    const e = toApiError(new HttpErrorResponse({ status: 0 }));
    expect(e.error).toBe('network');
    expect(e.message).toContain('last saved answers are kept');
  });

  it('keeps the server error code and message', () => {
    const e = toApiError(new HttpErrorResponse({ status: 409, error: { error: 'attempt_expired', message: 'Time is over.' } }));
    expect(e).toMatchObject({ status: 409, error: 'attempt_expired', message: 'Time is over.' });
  });

  it('exposes import reports as details', () => {
    const e = toApiError(new HttpErrorResponse({ status: 422, error: { error: 'import_invalid', report: { valid: false } } }));
    expect(e.details).toEqual({ valid: false });
  });
});
