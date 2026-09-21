# Local anime humanoid pipeline

## Primary path

1. Local character generation
   - Blender/Python generated anime humanoid as deterministic baseline
   - image-to-3D MLX path is evaluated separately
2. Skeleton
   - canonical Humanoid-like hierarchy
3. Motion
   - primary: HY-Motion 1.0 Lite text-to-motion
   - secondary: GVHMR video-to-motion on Apple Silicon
   - procedural clips are only the deterministic baseline/fallback for Unity integration
4. Retarget/bake
   - generated motion -> canonical skeleton -> FBX
5. Runtime
   - Unity 6.3, third-person action
6. Delivery
   - WebGL artifact only -> public GitHub Pages

## Acceptance

A milestone is not complete merely because a file was generated. The produced character must be loaded by Unity, animated at runtime, controllable in a WebGL build, and visually inspected from the published Pages build.
