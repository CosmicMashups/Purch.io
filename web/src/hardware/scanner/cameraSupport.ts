interface DetectedBarcode {
  rawValue: string;
}
export interface BarcodeDetectorLike {
  detect(source: CanvasImageSource): Promise<DetectedBarcode[]>;
}
export type BarcodeDetectorCtor = new (options?: { formats?: string[] }) => BarcodeDetectorLike;

export function detectorCtor(): BarcodeDetectorCtor | null {
  return (window as unknown as { BarcodeDetector?: BarcodeDetectorCtor }).BarcodeDetector ?? null;
}

/** The camera needs a secure page, the camera API, and the browser's built-in barcode detector (Chrome and Edge). */
export function cameraScanSupported(): boolean {
  return detectorCtor() !== null && typeof navigator.mediaDevices?.getUserMedia === 'function';
}
