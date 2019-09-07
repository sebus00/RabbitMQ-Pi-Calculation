using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQServer
{
    public class LongNumber
    {
        private UInt32[] number;
        private int size;
        private int maxDigits;

        public LongNumber(int maxDigits)
        {
            this.maxDigits = maxDigits;
            this.size = (int)Math.Ceiling((float)maxDigits * 0.104) + 2;
            number = new UInt32[size];
        }
        public LongNumber(int maxDigits, UInt32 intPart) : this(maxDigits)
        {
            number[0] = intPart;
            for (int i = 1; i < size; i++)
            {
                number[i] = 0;
            }
        }

        private void VerifySameSize(LongNumber value)
        {
            if (Object.ReferenceEquals(this, value))
                throw new Exception("LongNumbers cannot operate on themselves");
            if (value.size != this.size)
                throw new Exception("LongNumbers must have the same size");
        }

        public void Add(LongNumber value)
        {
            VerifySameSize(value);

            int index = size - 1;
            while (index >= 0 && value.number[index] == 0)
                index--;

            UInt32 carry = 0;
            while (index >= 0)
            {
                UInt64 result = (UInt64)number[index] +
                                value.number[index] + carry;
                number[index] = (UInt32)result;
                if (result >= 0x100000000U)
                    carry = 1;
                else
                    carry = 0;
                index--;
            }
        }

        public void Subtract(LongNumber value)
        {
            VerifySameSize(value);

            int index = size - 1;
            while (index >= 0 && value.number[index] == 0)
                index--;

            UInt32 borrow = 0;
            while (index >= 0)
            {
                UInt64 result = 0x100000000U + (UInt64)number[index] -
                                value.number[index] - borrow;
                number[index] = (UInt32)result;
                if (result >= 0x100000000U)
                    borrow = 0;
                else
                    borrow = 1;
                index--;
            }
        }

        public void Multiply(UInt32 value)
        {
            int index = size - 1;
            while (index >= 0 && number[index] == 0)
                index--;

            UInt32 carry = 0;
            while (index >= 0)
            {
                UInt64 result = (UInt64)number[index] * value + carry;
                number[index] = (UInt32)result;
                carry = (UInt32)(result >> 32);
                index--;
            }
        }

        public void Divide(UInt32 value)
        {
            int index = 0;
            while (index < size && number[index] == 0)
                index++;

            UInt32 carry = 0;
            while (index < size)
            {
                UInt64 result = number[index] + ((UInt64)carry << 32);
                number[index] = (UInt32)(result / (UInt64)value);
                carry = (UInt32)(result % (UInt64)value);
                index++;
            }
        }

        public void Assign(LongNumber value)
        {
            VerifySameSize(value);
            for (int i = 0; i < size; i++)
            {
                number[i] = value.number[i];
            }
        }

        public string Print()
        {
            LongNumber temp = new LongNumber(maxDigits);
            temp.Assign(this);

            StringBuilder sb = new StringBuilder();
            sb.Append(temp.number[0]);
            sb.Append(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator);

            int digitCount = 0;
            while (digitCount < maxDigits)
            {
                temp.number[0] = 0;
                temp.Multiply(100000);
                sb.AppendFormat("{0:D5}", temp.number[0]);
                digitCount += 5;
            }
            string result = sb.ToString();
            int rest = maxDigits % 5;
            if (rest != 0) result = result.Substring(0, result.Length - (5 - rest));
            return result;
        }

        public bool IsZero()
        {
            foreach (UInt32 item in number)
            {
                if (item != 0)
                    return false;
            }
            return true;
        }

        public async Task ArcTan(UInt32 multiplicand, UInt32 reciprocal, CancellationToken cancellationToken = default)
        {
            LongNumber X = new LongNumber(maxDigits, multiplicand);
            X.Divide(reciprocal);
            reciprocal *= reciprocal;

            this.Assign(X);

            LongNumber term = new LongNumber(maxDigits);
            UInt32 divisor = 1;
            bool subtractTerm = true;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                X.Divide(reciprocal);
                term.Assign(X);
                divisor += 2;
                term.Divide(divisor);
                if (term.IsZero())
                    break;

                if (subtractTerm)
                    this.Subtract(term);
                else
                    this.Add(term);
                subtractTerm = !subtractTerm;
            }
        }
    }
}
