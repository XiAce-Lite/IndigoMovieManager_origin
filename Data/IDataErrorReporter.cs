namespace IndigoMovieManager.Data
{
  internal interface IDataErrorReporter
  {
    void Report(string message, string title);
  }
}
