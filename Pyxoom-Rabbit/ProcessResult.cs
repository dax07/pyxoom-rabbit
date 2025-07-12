namespace Pyxoom_Rabbit
{

    public partial class RabbitMQHelper
    {
        public class ProcessResult
        {
            public ProcessResult(bool isSuccess, string msg)
            {
                this.IsSuccess = isSuccess;
                this.Msg = msg;
            }
            public ProcessResult(bool isSuccess, string msg, string data)
            {
                this.IsSuccess = isSuccess;
                this.Msg = msg;
                this.Data = data;
            }
            public ProcessResult() { }

            public bool IsSuccess { get; set; }
            public string Msg { get; set; }
            public string Data { get; set; }
        }
    }
}
