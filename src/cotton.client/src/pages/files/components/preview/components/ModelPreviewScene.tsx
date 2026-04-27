import { Canvas } from "@react-three/fiber";
import { Bounds, Environment, OrbitControls } from "@react-three/drei";
import { useTheme } from "@mui/material/styles";
import * as React from "react";
import * as THREE from "three";
import {
  type LightingPresetConfig,
  type PreparedModelScene,
} from "../modelPreviewTypes";

interface ModelPreviewSceneProps {
  gridLineColor: string;
  gridSubLineColor: string;
  lightingConfig: LightingPresetConfig;
  preparedModel: PreparedModelScene;
  sceneBackgroundColor: string;
  shadowsEnabled: boolean;
}

export const ModelPreviewScene: React.FC<ModelPreviewSceneProps> = ({
  gridLineColor,
  gridSubLineColor,
  lightingConfig,
  preparedModel,
  sceneBackgroundColor,
  shadowsEnabled,
}) => {
  const theme = useTheme();

  return (
    <Canvas
      flat
      shadows={shadowsEnabled ? { type: THREE.PCFShadowMap } : false}
      camera={{
        position: [2.5, 2.5, 2.5],
        fov: 45,
        near: 0.01,
        far: 1000,
      }}
      dpr={preparedModel.qualityMode === "reduced" ? [1, 1] : [1, 2]}
    >
      <color attach="background" args={[sceneBackgroundColor]} />
      <Environment preset="city" />
      <hemisphereLight
        args={[
          theme.palette.common.white,
          theme.palette.grey[900],
          lightingConfig.ambientIntensity,
        ]}
      />
      <directionalLight
        position={[9, 11, 8]}
        intensity={lightingConfig.keyIntensity}
        castShadow={shadowsEnabled}
      />
      <directionalLight
        position={[-7, 5, -6]}
        intensity={lightingConfig.fillIntensity}
        castShadow={false}
      />
      <directionalLight
        position={[0, 7, -10]}
        intensity={lightingConfig.rimIntensity}
        castShadow={false}
      />

      {shadowsEnabled && (
        <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, -0.012, 0]} receiveShadow>
          <planeGeometry args={[preparedModel.gridSize, preparedModel.gridSize]} />
          <shadowMaterial transparent opacity={0.28} />
        </mesh>
      )}

      <gridHelper
        args={[
          preparedModel.gridSize,
          preparedModel.gridDivisions,
          gridLineColor,
          gridSubLineColor,
        ]}
        position={[0, -0.01, 0]}
      />

      <Bounds fit clip margin={1.2}>
        <primitive object={preparedModel.object} />
      </Bounds>

      <OrbitControls makeDefault enableDamping dampingFactor={0.08} />
    </Canvas>
  );
};