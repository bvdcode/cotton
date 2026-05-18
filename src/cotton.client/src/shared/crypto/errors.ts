export class CryptoError extends Error {
  constructor(message: string) {
    super(message);
    this.name = new.target.name;
    Object.setPrototypeOf(this, new.target.prototype);
  }
}

export class InvalidCryptoInputError extends CryptoError {}

export class NoKeyError extends CryptoError {}

export type ClientEncryptionBlobOperation = "encrypt" | "decrypt";

export class ClientEncryptionSizeLimitError extends CryptoError {
  public readonly operation: ClientEncryptionBlobOperation;
  public readonly sizeBytes: number;
  public readonly maxBytes: number;

  constructor(
    operation: ClientEncryptionBlobOperation,
    sizeBytes: number,
    maxBytes: number,
  ) {
    super(
      `Client-side encrypted ${operation} is limited to ${maxBytes} bytes until streaming support is available.`,
    );
    this.operation = operation;
    this.sizeBytes = sizeBytes;
    this.maxBytes = maxBytes;
  }
}

export class UnsupportedVersionError extends CryptoError {}

export class NotAContainerError extends CryptoError {}

export class CorruptedContainerError extends CryptoError {}

export class WrongUnlockError extends CryptoError {}

export class InvalidRecoveryPhraseError extends CryptoError {}
