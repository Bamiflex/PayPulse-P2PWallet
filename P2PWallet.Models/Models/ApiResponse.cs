using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using P2PWallet.Models;

namespace P2PWallet.Models
{
    public class ApiResponse<T>
    {
        public bool Status { get; set; }
        public string StatusMessage { get; set; }
        public T Data { get; set; }

        public ApiResponse(bool status, string statusMessage, T data)
        {
            Status = status;
            StatusMessage = statusMessage;
            Data = data;
        }
    }
}
