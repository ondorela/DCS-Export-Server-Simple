using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

[StructLayout( LayoutKind.Sequential )]
public struct GlobalSegmentData
{
  public int Id;
  public int Cmd;
  public int Power;
  public float Yaw;
  public float Rot;
  public float Pit;
  public bool IsBuz;
  public int RightAmp;
  public int CenterAmp;
  public int LeftAmp;
  public int Hz;
}

namespace TCP_Server_Console
{
  class Program
  {
    // 与えられた文字列データをパースし、必要な情報を取り出す関数
    static (float, float) ParseDataAndCalculateTilt( string data )
    {
      // 正規表現を使用してデータを抽出
      var pitchMatch = Regex.Match( data, @"\[P\](-?\d+\.?\d*)" );
      var yawMatch = Regex.Match( data, @"\[Y\](-?\d+\.?\d*)" );
      var rollMatch = Regex.Match( data, @"\[R\](-?\d+\.?\d*)" );
      var afMatch = Regex.Match( data, @"\[AF\](-?\d+\.?\d*)" );
      var auMatch = Regex.Match( data, @"\[AU\](-?\d+\.?\d*)" );
      var asMatch = Regex.Match( data, @"\[AS\](-?\d+\.?\d*)" );
      var aapMatch = Regex.Match( data, @"\[AAP\](-?\d+\.?\d*)" );
      var aarMatch = Regex.Match( data, @"\[AAR\](-?\d+\.?\d*)" );
      //var aayMatch = Regex.Match( data, @"\[AAY\](-?\d+\.?\d*)" );

      // 抽出した文字列をfloatに変換
      float pitch = float.Parse( pitchMatch.Groups[1].Value );
      float yaw = float.Parse( yawMatch.Groups[1].Value ); // このサンプルでは使用しないが、将来的な使用のために抽出
      float roll = float.Parse( rollMatch.Groups[1].Value );

      float gf = float.Parse( afMatch.Groups[1].Value ); // 前後方向
      float gu = float.Parse( auMatch.Groups[1].Value ); // 上下方向
      float gs = float.Parse( asMatch.Groups[1].Value ); // 横方向

      float aap = float.Parse( aapMatch.Groups[1].Value ); // ピッチ
      float aar = float.Parse( aarMatch.Groups[1].Value ); // ロール
      //float aay = float.Parse( aayMatch.Groups[1].Value );

      // 抽出した値を使用して関数を呼び出す（仮の関数）
      return CalculateChairTiltFromFlightAndAcceleration( aap, aar, roll, pitch, gf, gs, gu );
    }

    // Math.Clampの簡易的な実装
    static float Clamp( float value, float min, float max )
    {
      if ( value < min ) return min;
      if ( value > max ) return max;
      return value;
    }

    static (float, float) CalculateChairTiltFromFlightAndAcceleration( float aap, float aar, float roll, float pitch,
      float accFront, float accSide, float accUp )
    {
      // 基準値と最大傾斜角度
      const float baseValue = 1.0f;
      const float maxTiltAngle = 30.0f; // 最大傾斜角度（度）

      // 滑らかさを調整するための係数
      const float smoothnessFactor = 1.0f;

      // 角加速度から滑らかな効果を計算
      float smoothEffectX = 1 - (float)Math.Exp( -Math.Abs( aar ) * smoothnessFactor );
      float smoothEffectY = 1 - (float)Math.Exp( -Math.Abs( aap ) * smoothnessFactor );

      // 角加速度に基づく傾斜角
      float tiltAngleX = smoothEffectX * baseValue * maxTiltAngle;
      float tiltAngleY = smoothEffectY * baseValue * maxTiltAngle;

      // ロールとピッチに基づく仮想的な重力の影響
      float virtualGravityEffectX = roll / 90.0f * smoothEffectX;
      float virtualGravityEffectY = pitch / 90.0f * smoothEffectY;

      // 横方向（GFX）と前方向（GFY）の加速度に基づく傾斜角の調整
      tiltAngleX += maxTiltAngle * virtualGravityEffectX - accSide * 5.0f; // 横方向のGファクター
      tiltAngleY += maxTiltAngle * virtualGravityEffectY - accFront * 5.0f; // 前方向のGファクター

      // 傾斜角を制限
      tiltAngleX = Clamp( tiltAngleX, -maxTiltAngle, maxTiltAngle );
      tiltAngleY = Clamp( tiltAngleY, -maxTiltAngle, maxTiltAngle );

      // 結果の表示
      Console.WriteLine( $"Calculated Chair Tilt - Roll: {tiltAngleX} degrees, Pitch: {tiltAngleY} degrees" );
      return ( tiltAngleX, tiltAngleY );
    }


    static void WriteToSharedMemory( string name, GlobalSegmentData data )
    {
      long size = Marshal.SizeOf( typeof(GlobalSegmentData) );

      try
      {
        using ( var mmf = MemoryMappedFile.OpenExisting( name ) )
        {
          using ( var accessor = mmf.CreateViewAccessor( 0, size ) )
          {
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal( (int)size );

            try
            {
              Marshal.StructureToPtr( data, ptr, false );
              Marshal.Copy( ptr, buffer, 0, buffer.Length );
              accessor.WriteArray( 0, buffer, 0, buffer.Length );
            }
            finally
            {
              Marshal.FreeHGlobal( ptr );
            }
          }
        }
      }
      catch ( FileNotFoundException )
      {
        // 共有メモリが存在しない場合、何もしない
        //Console.WriteLine("Shared memory does not exist, nothing to do.");
      }
    }


    static void Main()
    {
      TcpListener listener = new TcpListener( IPAddress.Parse( "127.0.0.1" ), 31090 );
      listener.Start();

      Console.WriteLine( "Server started." );

      while ( true )
      {
        Console.WriteLine( "Waiting for DCS connection..." );
        TcpClient client = listener.AcceptTcpClient();
        Console.WriteLine( "DCS connected :-)" );

        StreamReader reader = new StreamReader( client.GetStream() );
        StreamWriter writer = new StreamWriter( client.GetStream() );

        string s = string.Empty;

        while ( true )
        {
          s = reader.ReadLine();
          Console.WriteLine( s );

          // 文字列が'['で始まるかどうかを確認
          if ( s.StartsWith( "[" ) )
          {
            var tilt = ParseDataAndCalculateTilt( s );

            var data = new GlobalSegmentData
            {
              Id = 1,
              Cmd = 2,
              Power = 70,
              Yaw = 0.0f,
              Rot = tilt.Item1,
              Pit = tilt.Item2,
              IsBuz = false,
              RightAmp = 0,
              CenterAmp = 0,
              LeftAmp = 0,
              Hz = 0
            };
            WriteToSharedMemory( "MadYawSharedMemory", data );
          }

          if ( s == "exit" ) break;
        }

        reader.Close();
        writer.Close();
        client.Close();
      }
    } //End Main
  } //End Class
}