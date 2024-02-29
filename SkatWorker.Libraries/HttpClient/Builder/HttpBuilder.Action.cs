﻿using SkatWorker.Libraries.HttpClient.Infastructure;
using SkatWorker.Libraries.HttpClient.Providers.Interfaces;
using System;
using System.Net;

namespace SkatWorker.Libraries.HttpClient.Builder
{
  /// <summary>
  /// Конструктор действий запроса.
  /// </summary>
  public partial class HttpBuilder
  {
    /// <summary>
    /// Ссылка на выполняемый метод в случае успешного запроса.
    /// </summary>
    private Action<AppliedResponse> success;

    /// <summary>
    /// Ссылка на выполняемый метод в случае ошибки.
    /// </summary>
    private Action<WebException> fail;

    /// <summary>
    /// Провайдер выполняемых методов.
    /// </summary>
    public IActionProvider ActionProvider { get; set; }

    /// <summary>
    /// Делегат выполняемый в случае успешного запроса.
    /// </summary>
    /// <param name="action">Выполняемый метод.</param>
    /// <returns>Конструктор запроса.</returns>
    public HttpBuilder OnSuccess(Action<AppliedResponse> action)
    {
      this.success = action;

      return this;
    }

    /// <summary>
    /// Делегат выполняемый в случае ошибки запроса.
    /// </summary>
    /// <param name="action">Выполняемая метод.</param>
    /// <returns>Конструктор запроса.</returns>
    public HttpBuilder OnFail(Action<WebException> action)
    {
      this.fail = action;

      return this;
    }

    /// <summary>
    /// Добавить провайдер действий.
    /// </summary>
    /// <param name="provider">Провайдер действий.</param>
    public void SetActionProvider(IActionProvider provider)
    {
      this.ActionProvider = provider;
    }
  }
}
