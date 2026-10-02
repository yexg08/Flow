import { niceMax } from './metrics-page';

describe('niceMax', () => {
  it.each([
    [0, 4],
    [3, 4],
    [7, 10],
    [12, 20],
    [47, 50],
    [130, 200],
  ])('%i → %i', (value, expected) => {
    expect(niceMax(value)).toBe(expected);
  });
});
