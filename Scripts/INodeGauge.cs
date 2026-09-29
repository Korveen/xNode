namespace XNode
{
	public interface INodeGauge<TContext>
	{
		void GetGauge(TContext context, out float current, out float max);
	}
}
