import axios from "axios";

const AGENT_URL = process.env.NEXT_PUBLIC_AGENT_URL ?? "http://localhost:5000";

export const apiClient = axios.create({
  baseURL: AGENT_URL,
  timeout: 10_000,
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const status: number = error.response?.status ?? 0;
    const message: string =
      error.response?.data?.message ?? error.message ?? "Unknown error";
    return Promise.reject({ status, message });
  }
);
