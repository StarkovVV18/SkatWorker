﻿using SkatWorker.Libraries.HttpClient.Providers.Interfaces;
using SkatWorker.Libraries.HttpClient.Enums;
using System.IO;

namespace SkatWorker.Libraries.HttpClient.Providers.Body
{
  /// <summary>
  /// JSON провайдер тела запроса.
  /// </summary>
  public class JsonBodyProvider : IBodyProvider
  {
    /// <summary>
    /// <see cref="IBodyProvider.Body"/>
    /// </summary>
    public byte[] Body { get; set; }

    /// <summary>
    /// <see cref="IBodyProvider.PathToFile"/>
    /// </summary>
    public string PathToFile { get; set; }

    /// <summary>
    /// <see cref="IBodyProvider.ContentType"/>
    /// </summary>
    public string ContentType()
    {
      return Enums.ContentType.ApplicationJson;
    }

    /// <summary>
    /// <see cref="IBodyProvider.SetBody(Stream)"/>
    /// </summary>
    public void SetBody(Stream requestStream)
    {
      requestStream.Write(this.Body, 0, this.Body.Length);
    }
  }
}
