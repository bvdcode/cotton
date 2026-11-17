const nameCollator = new Intl.Collator(undefined, { numeric: true });

export const compareNames = (left: string, right: string): number =>
  nameCollator.compare(left, right);
