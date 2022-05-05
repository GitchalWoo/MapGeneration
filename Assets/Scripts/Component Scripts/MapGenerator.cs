using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using System.Linq;
using System.Collections.Concurrent;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode
    {
        NoiseMap, ColorMap, Mesh
    }
    public DrawMode drawMode;

    public GenerateNoise.NormalizeMode normalizeMode;

  
    public bool useFlatShading;
    
    [Range(0, 6)]
    public int editorLevelOfDetail;
    public float noiseScale;
    public int octaves;
    [Range(0, 1)]
    public float persistance;
    public float lacunarity;

    public int seed;
    public Vector2 offset;


    public float meshHightMultiplier;

    public AnimationCurve meshHightCurve;
    public bool autoUpdate;


    public TerrainType[] regions;

    ConcurrentQueue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new ConcurrentQueue<MapThreadInfo<MapData>>();
    ConcurrentQueue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new ConcurrentQueue<MapThreadInfo<MeshData>>();

    static MapGenerator instance;

    public static int mapChunkSize {
        get {
            if(instance == null) {
                instance = FindObjectOfType<MapGenerator>();
            }
            if(instance.useFlatShading) {
                return 95;
            } else {
                return 239;
            }
        }
    }

    public void DrawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);

        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(GenerateTexture.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.ColorMap)
        {
            display.DrawTexture(GenerateTexture.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(GenerateMesh.GenerateTerrainMesh(mapData.heightMap, meshHightMultiplier, meshHightCurve, editorLevelOfDetail, useFlatShading), GenerateTexture.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        }
    }


    public void RequestMapData(Vector2 center, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MapDataThread(center, callback);
        };

        new Thread(threadStart).Start();
    }

    void MapDataThread(Vector2 center, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(center);
        mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData, lod, callback);
        };

        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData = GenerateMesh.GenerateTerrainMesh(mapData.heightMap, meshHightMultiplier, meshHightCurve, lod, useFlatShading);
        meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
    }

    void Update()
    {
        MapThreadInfo<MapData> mapDataThreadInfo;
        while (mapDataThreadInfoQueue.TryDequeue(out mapDataThreadInfo))
        {
            mapDataThreadInfo.callback(mapDataThreadInfo.parameter);
        }

        MapThreadInfo<MeshData> meshDataThreadInfo;
        while (meshDataThreadInfoQueue.TryDequeue(out meshDataThreadInfo))
        {
            meshDataThreadInfo.callback(meshDataThreadInfo.parameter);
        }
    }

    MapData GenerateMapData(Vector2 center)
    {
        float[,] noiseMap = GenerateNoise.GenerateNoiseMap(mapChunkSize+2, mapChunkSize+2, noiseScale, octaves, persistance, lacunarity, seed, center + offset, normalizeMode);
        Color[] colorMap = GenerateColorMap(noiseMap);
        return new MapData(noiseMap, colorMap);
    }

    private Color[] GenerateColorMap(float[,] noiseMap)
    {
        Color[] colorMap = new Color[mapChunkSize * mapChunkSize];
        for (int y = 0; y < mapChunkSize; y++)
            for (int x = 0; x < mapChunkSize; x++)
            {
                float currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight >= regions[i].height)
                    {
                        colorMap[y * mapChunkSize + x] = regions[i].color;
                    }
                    else
                    {
                        break;
                    }
                }
            }

        return colorMap;
    }

    private void OnValidate()
    {
        if (noiseScale <= 0)
        {
            noiseScale = 0.0001f;
        }
        if (octaves < 0)
        {
            octaves = 0;
        }
        if (lacunarity < 1)
        {
            lacunarity = 1;
        }
    }


    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }


}

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
}

public struct MapData
{
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;

    public MapData(float[,] heightMap, Color[] colorMap)
    {
        this.heightMap = heightMap;
        this.colorMap = colorMap;
    }
}