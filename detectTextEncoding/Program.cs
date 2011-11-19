using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Numerics;
using System.Drawing;

namespace DetectTextEncoding
{
	abstract class DVector
	{
		abstract public double getScore(int i, int j);
		abstract public void exportC(string filename);
	}
	class DoubleVector : DVector
	{
		private readonly int size;
		private readonly double[,] vector;
		private readonly int factor;
		public DoubleVector(Vector vec)
		{
			this.size = vec.getSize();
			this.factor = 256 / size;
			vector = new double[size, size];
			double len = Math.Pow(Math.E, BigInteger.Log(vec.getLengthPowered()) / 2);
			for (int i = 0; i < size; i++)
				for (int j = 0; j < size; j++)
				{
					vector[i, j] = vec.GetScoreRaw(i, j) / len;
				}
		}
		public override double getScore(int i, int j)
		{
			return vector[i / factor, j / factor];
		}
		public override void exportC(string filename)
		{
			using (StreamWriter outFile = new StreamWriter(filename, false))
			{
				outFile.WriteLine("const double vector[" + this.size + "][" + this.size + "] = {");
				for (int i = 0; i < size; i++)
				{
					outFile.Write("\t{");
					for (int j = 0; j < size; j++)
					{
						outFile.Write(Convert.ToString(vector[i, j]) + ",");
					}
					outFile.WriteLine("},");
				}
				outFile.WriteLine("};");
			}
		}
	}
	class LogVector : DVector
	{
		private readonly int size;
		private readonly byte[,] vector;
		private readonly int factor;
		private readonly double powBase;
		public LogVector(Vector vec)
		{
			this.size = vec.getSize();
			this.factor = 256 / size;
			vector = new byte[size, size];
			ulong max = vec.getBiggest();
			double len = Math.Pow(Math.E, BigInteger.Log(vec.getLengthPowered()) / 2);
			powBase = max / len / 255; //そのまま
			//powBase = Math.Pow(Math.E, Math.Log((max / len) + 1) / 255); //対数
			//powBase = Math.Pow(Math.E, Math.Log(Math.Log((max / len) + 1) + 1) / 255); //二重対数
			double delta = 0.0f;
			double total = 0.0f;
			for (int i = 0; i < size; i++)
				for (int j = 0; j < size; j++)
				{
					double dval = (double)vec.GetScoreRaw(i, j) / len;
					byte val = (byte)Math.Round(dval * 255 / powBase); //そのまま
					//byte val = (byte)Math.Round(Math.Log(dval + 1, powBase)); //対数
					//byte val = (byte)Math.Round(Math.Log(Math.Log(dval + 1) + 1, powBase)); //二重対数
					vector[i, j] = val;
					if(dval > 0.0f){
						delta += Math.Abs(dval - getScore(i * factor, j * factor)) * 100;
						total += dval;
					}
				}
			Console.WriteLine("AverageDelta: " + (delta / total) + "%");
		}
		public override double getScore(int i, int j)
		{
			return vector[i / factor, j / factor] * powBase / 255;//そのまま
			//return Math.Pow(powBase, vector[i / factor, j / factor]) - 1; //対数
			//return Math.Pow(Math.E, Math.Pow(powBase, vector[i / factor, j / factor]) - 1) - 1; //二重対数
		}
		public override void exportC(string filename)
		{
			using (StreamWriter outFile = new StreamWriter(filename, false))
			{
				outFile.WriteLine("const double base="+Convert.ToString(powBase)+";");
				outFile.WriteLine("const unsigned char vector[" + this.size + "][" + this.size + "] = {");
				for (int i = 0; i < size; i++)
				{
					outFile.Write("\t{");
					for (int j = 0; j < size; j++)
					{
						outFile.Write(Convert.ToString(vector[i, j]) + ",");
					}
					outFile.WriteLine("},");
				}
				outFile.WriteLine("};");
			}
		}
	}
	class Vector
	{
		private readonly int size;
		private readonly int factor;
		private readonly ulong[,] vector;
		private Vector(int size)
		{
			this.size = size;
			this.factor = 256 / size;
			vector = new ulong[size, size];
			for (int i = 0; i < size; i++)
				for (int j = 0; j < size; j++)
				{
					vector[i, j] = 0;
				}
		}
		public Vector(string path)
		{
			size = (int)Math.Sqrt((new FileInfo(path)).Length/4);
			this.factor = 256 / size;
			vector = new ulong[size, size];
			ulong max = 0;
			using (FileStream fstr = new FileStream(path, FileMode.Open))
			{
				using (BinaryReader reader = new BinaryReader(fstr))
				{
					for (int i = 0; i < size; i++)
						for (int j = 0; j < size; j++)
						{
							vector[i, j] = reader.ReadUInt32();
							max = Math.Max(max, vector[i, j]);
						}
				}
			}
		}
		public Vector downScale(int to)
		{
			if(size % to != 0){
				throw new Exception();
			}
			if(size == to){
				return this;
			}
			int factor = (int)(size / to);
			Vector vec = new Vector(to);
			for (int i = 0; i < size; i++)
				for (int j = 0; j < size; j++)
				{
					vec.vector[i / factor, j / factor] += vector[i, j];
				}
			return vec;
		}
		private BigInteger length = 0;
		public BigInteger getLengthPowered()
		{
			if (length == 0)
			{
				BigInteger sum = 0;
				for (int i = 0; i < size; i++)
					for (int j = 0; j < size; j++)
					{
						BigInteger uval = vector[i, j];
						sum += (uval * uval);
					}
				length = sum;
			}
			return length;
		}
		private BigInteger total = 0;
		public BigInteger getCount()
		{
			if (total == 0)
			{
				BigInteger sum = 0;
				for (int i = 0; i < size; i++)
					for (int j = 0; j < size; j++)
					{
						sum += vector[i, j];
					}
				total = sum;
			}
			return total;
		}
		private ulong biggest = ulong.MinValue;
		public ulong getBiggest()
		{
			if (biggest == ulong.MinValue)
			{
				ulong big = ulong.MinValue;
				for (int i = 0; i < size; i++)
					for (int j = 0; j < size; j++)
					{
						big = Math.Max(vector[i, j], big);
					}
				biggest = big;
			}
			return biggest;
		}
		private ulong smallest = ulong.MaxValue;
		public ulong getSmallest()
		{
			if (smallest == ulong.MaxValue)
			{
				ulong small = ulong.MaxValue;
				for (int i = 0; i < size; i++)
					for (int j = 0; j < size; j++)
					{
						if (vector[i, j] > 0)
						{
							small = Math.Min(vector[i, j], small);
						}
					}
				smallest = small;
			}
			return smallest;
		}
		public ulong GetScore(int i, int j)
		{
			return vector[i / factor, j / factor];
		}
		public ulong GetScoreRaw(int i, int j)
		{
			return vector[i , j];
		}
		public Vector mask(Vector mask)
		{
			if (mask.size != this.size)
			{
				throw new Exception();
			}
			Vector vec = new Vector(this.size);
			for (int i = 0; i < size; i++)
				for (int j = 0; j < size; j++)
				{
					if (mask.vector[i, j] != 0)
					{
						vec.vector[i, j] = vector[i, j];
					}
					else
					{
						vec.vector[i, j] = 0;
					}
				}
			return vec;
		}
		public Vector maskInvert(Vector mask)
		{
			Vector vec = new Vector(this.size);
			if (mask.size != this.size)
			{
				throw new Exception();
			}
			for (int i = 0; i < size; i++)
				for (int j = 0; j < size; j++)
				{
					if (mask.vector[i, j] == 0)
					{
						vec.vector[i, j] = vector[i, j];
					}
					else
					{
						vec.vector[i, j] = 0;
					}
				}
			return vec;
		}
		public int getSize()
		{
			return size;
		}
		public void exportCSV(string name){
			using (StreamWriter outFile = new StreamWriter(name, false))
			{
				outFile.Write(",");
				for (int i = 1; i <= 100; i++)
				{
					outFile.Write("" + i + ",");
				}
				outFile.WriteLine();
				ulong[] cnt = new ulong[100];
				ulong[] cntLog1 = new ulong[100];
				ulong[] cntLog2 = new ulong[100];
				ulong total = 0;
				ulong max = getBiggest();
				double maxLog1 = Math.Log(getBiggest() + 1);
				double maxLog2 = Math.Log(maxLog1 + 1);
				foreach (ulong v in vector)
				{
					if (v == max || v == 0)
					{
						continue;
					}
					cnt[(v * 100 / max) + 1]++;
					cntLog1[(int)(Math.Log(v + 1) * 100 / maxLog1)]++;
					cntLog2[(int)(Math.Log(Math.Log(v + 1) + 1) * 100 / maxLog2)]++;
					total++;
				}
				outFile.Write("cnt,");
				for (int i = 1; i <= 100; i++)
				{
					outFile.Write("=" + (cnt[i - 1]*100) + "/" + total + ",");
				}
				outFile.WriteLine();
				outFile.Write("Log,");
				for (int i = 1; i <= 100; i++)
				{
					outFile.Write("=" + (cntLog1[i - 1] * 100) + "/" + total + ",");
				}
				outFile.WriteLine();
				outFile.Write("Log2,");
				for (int i = 1; i <= 100; i++)
				{
					outFile.Write("=" + (cntLog2[i - 1] * 100) + "/" + total + ",");
				}
				outFile.WriteLine();
			}
		}
		private static Color CreateColorFromHSV(float h, float s, float v)
		{
			if (s == 0.0f)
			{
				return Color.Black;
			}
			h = (h % 360 + 360) % 360;
			int range = (int)h / 60;
			float f = h / 60 - range;
			float p = v * (1 - s);
			float q = v * (1 - f * s);
			float t = v * (1 - (1 - f) * s);
			switch (range)
			{
				case 0:
					return Color.FromArgb((int)(v * 255), (int)(t * 255), (int)(p * 255));
				case 1:
					return Color.FromArgb((int)(q * 255), (int)(v * 255), (int)(p * 255));
				case 2:
					return Color.FromArgb((int)(p * 255), (int)(v * 255), (int)(t * 255));
				case 3:
					return Color.FromArgb((int)(p * 255), (int)(q * 255), (int)(v * 255));
				case 4:
					return Color.FromArgb((int)(t * 255), (int)(p * 255), (int)(v * 255));
				case 5:
					return Color.FromArgb((int)(v * 255), (int)(p * 255), (int)(q * 255));
				default:
					throw new Exception("Invalid range:" + h);
			}
		}
		public void exportImage(string filename)
		{
			Bitmap bmp = new Bitmap(256, 256);
			ulong max = 1;
			foreach (ulong count in vector)
			{
				max = Math.Max(max, count);
			}
			for (int x = 0; x < 256; x++)
				for (int y = 0; y < 256; y++)
				{
					double param = Math.Log(vector[x / factor, y / factor] + 1, max + 1);
					bmp.SetPixel(x, y, CreateColorFromHSV((float)(330.0f * param) + 240, 1, 1));
				}
			bmp.Save(filename);
		}
	}
	class Tester
	{
		private readonly Dictionary<string, Vector> vecDictiory;
		private readonly Dictionary<string, DVector> doubleDictiory;
		private readonly FileSet fileSet;
		private readonly string encode;
		private readonly BigInteger factor;
		public Tester(Dictionary<string, Vector> vecDictionary, Dictionary<string, DVector> doubleDictiory, FileSet fileSet, string encode)
		{
			this.vecDictiory = vecDictionary;
			this.doubleDictiory = doubleDictiory;
			this.fileSet = fileSet;
			this.encode = encode;
			this.factor = 1;
			foreach (KeyValuePair<string, Vector> pair in this.vecDictiory)
			{
				factor *= pair.Value.getLengthPowered();
			}
			factor *= factor;
		}
		private bool isAscii(int chr)
		{
			return (chr >= 0x20 && chr <= 0x7e) || chr == 0x09 || chr == 0x0a || chr == 0x0c || chr == 0x0d;
		}

		private void doEachTest(string filename, ulong length, out bool result, out bool dresult, out bool extResult)
		{
			Dictionary<string, BigInteger> scoreDic = new Dictionary<string, BigInteger>();
			Dictionary<string, Double> dscoreDic = new Dictionary<string, Double>();
			List<sbyte> data = new List<sbyte>(); //ExtLib用
			foreach (KeyValuePair<string, Vector> pair in this.vecDictiory)
			{
				scoreDic.Add(pair.Key, 0);
				dscoreDic.Add(pair.Key, 0.0f);
			}
			using (FileStream fstr = new FileStream(filename, FileMode.Open))
			{
				int i = fstr.ReadByte();
				data.Add((sbyte)i);
				int j = 0;
				ulong charCount = 0;
				while ((j = fstr.ReadByte()) > 0)
				{
					data.Add((sbyte)j);
					if (!(isAscii(i) && isAscii(j)))
					{
						bool nonZero = false;
						foreach (KeyValuePair<string, Vector> pair in this.vecDictiory)
						{
							ulong score = pair.Value.GetScore(i, j);
							double dscore = doubleDictiory[pair.Key].getScore(i, j);
							if (score > 0)
							{
								scoreDic[pair.Key] += score;
								dscoreDic[pair.Key] += dscore;
								nonZero |= true;
							}
						}
						if (nonZero)
						{
							charCount++;
							if (charCount >= length)
							{
								break;
							}
						}
					}
					i = j;
				}
				{ /* Non-Double */
					string answer = "ascii";
					BigInteger max = BigInteger.Zero;
					foreach (KeyValuePair<string, BigInteger> pair in scoreDic)
					{
						// ひたすら正確さ重視。
						// 内積の二乗を比較し、さらに浮動小数点は使わない。
						// 大小関係さえ分かればいいので、実際そこまで精度が重要とは思えないのですが…。
						BigInteger val = BigInteger.Divide(pair.Value * pair.Value * this.factor, vecDictiory[pair.Key].getLengthPowered());
						if (val > max)
						{
							answer = pair.Key;
							max = val;
						}
					}
					result = answer.Equals(encode);
				}
				{ /* Double */
					string answer = "ascii";
					Double max = 0.0f;
					foreach (KeyValuePair<string, Double> pair in dscoreDic)
					{
						if (pair.Value > max)
						{
							answer = pair.Key;
							max = pair.Value;
						}
					}
					dresult = answer.Equals(encode);
				}
			}
			{
				sbyte[] adata = data.ToArray();
				switch (ext.KanjiCode.judge(adata, adata.Length))
				{
					case ext.KanjiCode.Type.EUC:
						extResult = encode.Equals("euc");
						break;
					case ext.KanjiCode.Type.JIS:
						extResult = encode.Equals("jis");
						break;
					case ext.KanjiCode.Type.SJIS:
						extResult = encode.Equals("sjis");
						break;
					case ext.KanjiCode.Type.UTF8:
						extResult = encode.Equals("utf8");
						break;
					default:
						extResult = false;
						break;
				};
			}
		}
		public void test(ulong length, out ulong score, out ulong dscore, out ulong extScore, out ulong total)
		{
			ulong _score = 0;
			ulong _dscore = 0;
			ulong _extScore = 0;
			ulong _total = 0;
			fileSet.ForEach(delegate(string path)
			{
				bool result;
				bool dresult;
				bool extResult;
				doEachTest(path, length, out result, out dresult, out extResult);
				if (result)
				{
					_score++;
				}
				if (dresult)
				{
					_dscore++;
				}
				if (extResult)
				{
					_extScore++;
				}
				_total++;
			});
			score = _score;
			dscore = _dscore;
			extScore = _extScore;
			total = _total;
		}
	}
	class FileSet
	{
		private List<string> fileList = new List<string>();
		public FileSet(string path)
		{
			addPath(path);
		}
		private void addPath(string path)
		{
			if (Directory.Exists(path))
			{
				foreach(string file in Directory.EnumerateFiles(path)){
					fileList.Add(file);
				}
				foreach (string dir in Directory.EnumerateDirectories(path))
				{
					addPath(dir);
				}
			}
			else if (File.Exists(path))
			{
				fileList.Add(path);
			}
		}
		public int Size()
		{
			return fileList.Count;
		}
		public void ForEach(Action<string> action)
		{
			fileList.ForEach(action);
		}
	}
	class DetectTextEncoding
	{
		private Dictionary<string, FileSet> fileDic = new Dictionary<string, FileSet>();
		public DetectTextEncoding()
		{
			fileDic.Add("sjis",new FileSet("./sjis"));
			fileDic.Add("euc",new FileSet("./euc"));
			fileDic.Add("jis", new FileSet("./jis"));
			fileDic.Add("utf8", new FileSet("./utf8"));
		}
		void test(int to)
		{
			Dictionary<string, List<string>> resultDic = new Dictionary<string, List<string>>();
			resultDic.Add("mokuji", new List<string>());
			foreach (KeyValuePair<string, FileSet> pair in fileDic)
			{
				resultDic.Add(pair.Key, new List<string>());
			}
			resultDic.Add("total", new List<string>());
			foreach (KeyValuePair<string, FileSet> pair in fileDic)
			{
				resultDic.Add(pair.Key + "_DBL", new List<string>());
				resultDic.Add(pair.Key + "_EXT", new List<string>());
			}
			resultDic.Add("total_DBL", new List<string>());
			resultDic.Add("total_EXT", new List<string>());

			Console.WriteLine("doing test size:" + to);
			Dictionary<string, Vector> vectorDic = new Dictionary<string, Vector>();
			Dictionary<string, DVector> doubleDic = new Dictionary<string, DVector>();
			{
				Vector mask = new Vector("./dic/mask.vec");
				{
					vectorDic.Add("sjis", new Vector("./dic/sjis.vec").mask(mask).downScale(to));
					vectorDic.Add("euc", new Vector("./dic/euc.vec").mask(mask).downScale(to));
					vectorDic.Add("jis", new Vector("./dic/jis.vec").mask(mask).downScale(to));
					vectorDic.Add("utf8", new Vector("./dic/utf8.vec").mask(mask).downScale(to));
					foreach (KeyValuePair<string, Vector> pair in vectorDic)
					{
						doubleDic.Add(pair.Key, new LogVector(pair.Value));
						doubleDic[pair.Key].exportC("./" + pair.Key + ".c");
					}
				}
			}
			for (ulong length = 1; length <= 100; length++)
			{
				Console.WriteLine("doing test stage:" + to + "-" + length);
				resultDic["mokuji"].Add(Convert.ToString(length));
				ulong allTotal = 0;
				ulong allExtTotal = 0;
				ulong allDScore = 0;
				ulong allScore = 0;
				ulong allExtScore = 0;
				foreach (KeyValuePair<string, FileSet> pair in fileDic)
				{
					ulong score;
					ulong dscore;
					ulong extScore;
					ulong total;
					new Tester(vectorDic, doubleDic, pair.Value, pair.Key).test(length, out score, out dscore, out extScore, out total);
					allTotal += total;
					allDScore += dscore;
					allScore += score;
					allExtScore += extScore;
					if (extScore > 0)
					{
						allExtTotal += total;
					}
					resultDic[pair.Key].Add("=" + score + "/" + total);
					resultDic[pair.Key + "_DBL"].Add("=" + dscore + "/" + total);
					resultDic[pair.Key + "_EXT"].Add("=" + score + "/" + total);
					Console.WriteLine("stage:" + to + "-" + length + " enc:" + pair.Key + "     score:" + score + "/" + total + "=" + ((float)score / total));
					Console.WriteLine("stage:" + to + "-" + length + " enc:" + pair.Key + "_DBL score:" + dscore + "/" + total + "=" + ((float)dscore / total));
					Console.WriteLine("stage:" + to + "-" + length + " enc:" + pair.Key + "_EXT score:" + extScore + "/" + total + "=" + ((float)extScore / total));
				}
				resultDic["total"].Add("=" + allScore + "/" + allTotal);
				resultDic["total_DBL"].Add("=" + allDScore + "/" + allTotal);
				resultDic["total_EXT"].Add("=" + allExtScore + "/" + allExtTotal);
				Console.WriteLine("stage:" + to + "-" + length + " enc:total     score:" + allScore + "/" + allTotal + "=" + ((float)allScore / allTotal));
				Console.WriteLine("stage:" + to + "-" + length + " enc:total_DBL score:" + allDScore + "/" + allTotal + "=" + ((float)allDScore / allTotal));
				Console.WriteLine("stage:" + to + "-" + length + " enc:total_EXT score:" + allExtScore + "/" + allExtTotal + "=" + ((float)allExtScore / allExtTotal));
			}
			using (StreamWriter outFile = new StreamWriter("./estimate_" + to + ".csv", false))
			{
				outFile.Write(",");
				foreach (string val in resultDic["mokuji"])
				{
					outFile.Write(val + ",");
				}
				outFile.WriteLine();
				foreach (KeyValuePair<string, List<string>> pair in resultDic)
				{
					if (pair.Key.Equals("mokuji"))
					{
						continue;
					}
					outFile.Write(pair.Key + ",");
					foreach (string val in pair.Value)
					{
						outFile.Write(val + ",");
					}
					outFile.WriteLine();
				}
			}
		}
		static void Main(string[] args)
		{
			DetectTextEncoding encoding = new DetectTextEncoding();
			for (int i = 8; i >= 8;i>>=1 )
			{
				encoding.test(i);
			}
		}
	}
}
