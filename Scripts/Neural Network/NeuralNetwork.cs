using System;
using System.Collections.Generic;
using System.IO;
using Random = UnityEngine.Random;

public class NeuralNetwork : IComparable<NeuralNetwork>
{
    // Ripped from https://towardsdatascience.com/building-a-neural-network-framework-in-c-16ef56ce1fef

    int[] layers;
    float[][] neurons;
    float[,] Neurons;
    float[][] biases;
    float[][][] weights;
    public enum Activations
    {
        Sigmoid,
        Tanh,
        ReLU,
        LeakyReLU
    }

    Activations activation;

    public float fitness = 0;

    public NeuralNetwork(int[] layers, Activations layerActivation)
    {
        this.layers = new int[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            this.layers[i] = layers[i];
        }

        activation = layerActivation;

        InitNeurons();
        InitBiases();
        InitWeights();
    }

    void InitNeurons()
    {
        List<float[]> neuronsList = new List<float[]>();
        for (int i = 0; i < layers.Length; i++)
        {
            neuronsList.Add(new float[layers[i]]);
        }
        neurons = neuronsList.ToArray();
    }

    void InitBiases()
    {
        List<float[]> biasList = new List<float[]>();
        for (int i = 0; i < layers.Length; i++)
        {
            float[] bias = new float[layers[i]];
            for (int j = 0; j < layers[i]; j++)
            {
                bias[j] = Random.Range(-0.5f, 0.5f);
            }
            biasList.Add(bias);
        }
        biases = biasList.ToArray();
    }

    void InitWeights()
    {
        List<float[][]> weightsList = new List<float[][]>();
        for (int i = 1; i < layers.Length; i++)
        {
            List<float[]> layerWeightsList = new List<float[]>();
            int neuronsInPreviousLayer = layers[i - 1];
            for (int j = 0; j < neurons[i].Length; j++)
            {
                float[] neuronWeights = new float[neuronsInPreviousLayer];
                for (int k = 0; k < neuronsInPreviousLayer; k++)
                {
                    neuronWeights[k] = Random.Range(-0.5f, 0.5f);
                }
                layerWeightsList.Add(neuronWeights);
            }
            weightsList.Add(layerWeightsList.ToArray());
        }
        weights = weightsList.ToArray();
    }

    public float Sigmoid(float x)//activation functions and their corrosponding derivatives
    {
        float k = (float)Math.Exp(x);
        return k / (1.0f + k);
    }
    public float Tanh(float x)
    {
        return (float)Math.Tanh(x);
    }
    public float Relu(float x)
    {
        return (0 >= x) ? 0 : x;
    }
    public float Leakyrelu(float x)
    {
        return (0 >= x) ? 0.01f * x : x;
    }
    public float SigmoidDer(float x)
    {
        return x * (1 - x);
    }
    public float TanhDer(float x)
    {
        return 1 - (x * x);
    }
    public float ReluDer(float x)
    {
        return (0 >= x) ? 0 : 1;
    }
    public float LeakyreluDer(float x)
    {
        return (0 >= x) ? 0.01f : 1;
    }

    public float Activate(float value, Activations layer)
    {
        /* Some popular variants of the activation function:
         * 1. Identity
         * 2. Binary Step
         * 3. Sigmoid
         * 4. Tanh
         * 5. ReLU
         * 6. Leaky ReLU
         * 7. Softmax
        */

        // tanh(x) = 2 / ((1+e^-2x) - 1)

        return layer switch
        {
            Activations.Sigmoid => Sigmoid(value),
            Activations.Tanh => Tanh(value),
            Activations.ReLU => Relu(value),
            Activations.LeakyReLU => Leakyrelu(value),
            _ => Relu(value),
        };
    }

    public float ActivateDer(float value, Activations layer)
    {
        return layer switch
        {
            Activations.Sigmoid => SigmoidDer(value),
            Activations.Tanh => TanhDer(value),
            Activations.ReLU => ReluDer(value),
            Activations.LeakyReLU => LeakyreluDer(value),
            _ => ReluDer(value),
        };
    }

    // inputs to outputs
    public float[] Forward(float[] inputs)
    {
        for (int i = 0; i < inputs.Length; i++)
        {
            neurons[0][i] = inputs[i];
        }
        for (int i = 1; i < layers.Length; i++)
        {
            int layer = i - 1;
            for (int j = 0; j < neurons[i].Length; j++)
            {
                float value = 0;
                for (int k = 0; k < neurons[layer].Length; k++)
                {
                    value += weights[layer][j][k] * neurons[layer][k];
                }
                neurons[i][j] = Activate(value + biases[i][j], activation);
            }
        }
        return neurons[^1];
    }

    public void BackPropogate()
    {

    }

    public void Mutate(float chance, float val)
    {
        for (int i = 0; i < biases.Length; i++)
        {
            for (int j = 0; j < biases[i].Length; j++)
            {
                biases[i][j] = Random.value < chance ? biases[i][j] += Random.Range(-val, val) : biases[i][j];
            }
        }

        for (int i = 0; i < weights.Length; i++)
        {
            for (int j = 0; j < weights[i].Length; j++)
            {
                for (int k = 0; k < weights[i][j].Length; k++)
                {
                    weights[i][j][k] = Random.value < chance ? weights[i][j][k] += Random.Range(-val, val) : weights[i][j][k];
                }
            }
        }
    }

    public int CompareTo(NeuralNetwork other)
    {
        if (other == null)
        {
            return 1;
        }
        if (fitness > other.fitness)
        {
            return 1;
        }
        else if (fitness < other.fitness)
        {
            return -1;
        }
        else
        {
            return 0;
        }
    }

    public NeuralNetwork Copy(NeuralNetwork into)
    {
        for (int i = 0; i < biases.Length; i++)
        {
            for (int j = 0; j < biases[i].Length; j++)
            {
                into.biases[i][j] = biases[i][j];
            }
        }
        for (int i = 0; i < weights.Length; i++)
        {
            for (int j = 0; j < weights[i].Length; j++)
            {
                for (int k = 0; k < weights[i][j].Length; k++)
                {
                    into.weights[i][j][k] = weights[i][j][k];
                }
            }
        }
        return into;
    }

    public void Save(string path)
    {
        File.WriteAllText(path, string.Empty);
        StreamWriter writer = new StreamWriter(path, true);
        for (int i = 0; i < biases.Length; i++)
        {
            for (int j = 0; j < biases[i].Length; j++)
            {
                writer.WriteLine(biases[i][j]);
            }
        }
        for (int i = 0; i < weights.Length; i++)
        {
            for (int j = 0; j < weights[i].Length; j++)
            {
                for (int k = 0; k < weights[i][j].Length; k++)
                {
                    writer.WriteLine(weights[i][j][k]);
                }
            }
        }
        writer.Close();
    }

    public void Load(string path)
    {
        TextReader tr = new StreamReader(path);
        int NumberOfLines = (int)new FileInfo(path).Length;
        string[] ListLines = new string[NumberOfLines];
        int index = 1;
        for (int i = 1; i < NumberOfLines; i++)
        {
            ListLines[i] = tr.ReadLine();
        }
        tr.Close();
        if (new FileInfo(path).Length > 0)
        {
            for (int i = 0; i < biases.Length; i++)
            {
                for (int j = 0; j < biases[i].Length; j++)
                {
                    biases[i][j] = float.Parse(ListLines[index]);
                    index++;
                }
            }

            for (int i = 0; i < weights.Length; i++)
            {
                for (int j = 0; j < weights[i].Length; j++)
                {
                    for (int k = 0; k < weights[i][j].Length; k++)
                    {
                        weights[i][j][k] = float.Parse(ListLines[index]);
                        index++;
                    }
                }
            }
        }
    }
}
