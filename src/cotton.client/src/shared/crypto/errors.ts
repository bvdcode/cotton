export class CryptoError extends Error {
  constructor(message: string) {
    super(message);
    this.name = new.target.name;
    Object.setPrototypeOf(this, new.target.prototype);
  }
}

export class InvalidCryptoInputError extends CryptoError {}

export class NoKeyError extends CryptoError {}

export class UnsupportedVersionError extends CryptoError {}

export class NotAContainerError extends CryptoError {}

export class CorruptedContainerError extends CryptoError {}

export class WrongUnlockError extends CryptoError {}

export class InvalidRecoveryPhraseError extends CryptoError {}
