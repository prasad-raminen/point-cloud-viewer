import {
  decodePointBuffer,
  parseOrigin,
  parseSampling,
  type DecodedPoints,
  type Vec3,
} from '../lib/pointBuffer';

/** Mirrors PointCloudDto in the ASP.NET Core API. */
export interface PointCloudDto {
  id: string;
  name: string;
  sourceFileName: string;
  format: string;
  pointCount: number;
  lodPointCount: number;
  lodVoxelSize: number | null;
  hasColor: boolean;
  bounds: { min: Vec3; max: Vec3; size: Vec3 };
  centroid: Vec3;
  origin: Vec3;
  skippedLines: number;
  warnings: string[];
  sourceSizeBytes: number;
  isSample: boolean;
  createdAtUtc: string;
}

/** Error carrying the RFC 7807 problem details returned by the API. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly title?: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

const baseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? '';

async function ensureOk(response: Response): Promise<Response> {
  if (response.ok) return response;
  let title: string | undefined;
  let detail: string | undefined;
  try {
    const problem = (await response.json()) as {
      title?: string;
      detail?: string;
      errors?: Record<string, string[]>;
    };
    title = problem.title;
    detail = problem.detail ?? Object.values(problem.errors ?? {}).flat()[0];
  } catch {
    // Body was not JSON; fall back to the status text.
  }
  throw new ApiError(detail ?? title ?? response.statusText, response.status, title);
}

export async function listClouds(signal?: AbortSignal): Promise<PointCloudDto[]> {
  const response = await ensureOk(await fetch(`${baseUrl}/api/clouds`, { signal }));
  return (await response.json()) as PointCloudDto[];
}

export async function uploadCloud(file: File, name?: string): Promise<PointCloudDto> {
  const form = new FormData();
  form.append('file', file);
  if (name) form.append('name', name);
  const response = await ensureOk(await fetch(`${baseUrl}/api/clouds`, { method: 'POST', body: form }));
  return (await response.json()) as PointCloudDto;
}

export async function deleteCloud(id: string): Promise<void> {
  await ensureOk(await fetch(`${baseUrl}/api/clouds/${encodeURIComponent(id)}`, { method: 'DELETE' }));
}

export async function fetchPoints(
  id: string,
  maxPoints: number,
  signal?: AbortSignal,
): Promise<DecodedPoints> {
  const url = `${baseUrl}/api/clouds/${encodeURIComponent(id)}/points?maxPoints=${maxPoints}`;
  const response = await ensureOk(await fetch(url, { signal }));
  const buffer = await response.arrayBuffer();
  const total = Number.parseInt(response.headers.get('X-Point-Total-Count') ?? '', 10);
  return decodePointBuffer(buffer, {
    origin: parseOrigin(response.headers.get('X-Point-Origin')),
    sampling: parseSampling(response.headers.get('X-Point-Sampling')),
    totalCount: Number.isFinite(total) ? total : undefined,
  });
}
