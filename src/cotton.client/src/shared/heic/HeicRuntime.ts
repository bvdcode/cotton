export interface HeicDecodedPixels {
  width: number;
  height: number;
  pixels: Uint8Array;
}

interface HeicAssemblyExports {
  Cotton: {
    Heic: {
      Browser: {
        HeicDecoder: { Decode: (bytes: Uint8Array) => HeicDecodedPixels };
      };
    };
  };
}

interface HeicRuntime {
  setModuleImports: (
    name: string,
    imports: {
      createImage: (
        width: number,
        height: number,
        pixels: Uint8Array,
      ) => HeicDecodedPixels;
    },
  ) => void;
  getConfig: () => { mainAssemblyName: string };
  getAssemblyExports: (name: string) => Promise<HeicAssemblyExports>;
}

export interface HeicRuntimeModule {
  dotnet: { create: () => Promise<HeicRuntime> };
}
